using Identity.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Shared.Contracts;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;

namespace Identity.API;

public static class IdentityEndpoints
{
    // Verifies that a byte header matches a known image format signature.
    // Guards against MIME type spoofing — file.ContentType is client-supplied and untrustworthy.
    static bool HasValidImageMagicBytes(byte[] header)
    {
        // JPEG: FF D8 FF
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return true;
        // PNG: 89 50 4E 47
        if (header.Length >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return true;
        // GIF87a / GIF89a: 47 49 46 38
        if (header.Length >= 4 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
            return true;
        // WebP: RIFF????WEBP (bytes 0-3 = RIFF, 8-11 = WEBP)
        if (header.Length >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return true;
        // HEIC/HEIF: ISO base media file — 'ftyp' box marker at byte offset 4
        if (header.Length >= 8 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70)
            return true;
        return false;
    }

    // ProfileImagePath is stored as "{guid}/{filename}" — the GUID subdirectory
    // acts as a natural version token that changes on every upload, so browsers
    // cache the URL safely and re-fetch automatically when the avatar is replaced.
    static string? AvatarUrl(Guid userId, string? profileImagePath)
    {
        if (profileImagePath is null) return null;
        var version = profileImagePath.Split('/')[0];
        return $"/api/users/{userId}/avatar?v={version}";
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        // Public — initiates browser OIDC redirect to Authentik
        app.MapGet("/auth/login", (HttpContext ctx) =>
        {
            var props = new AuthenticationProperties
            {
                RedirectUri = "/auth/callback",
            };
            return Results.Challenge(props, ["OpenIdConnect"]);
        }).AllowAnonymous();

        // Public — terminates the Authentik SSO session via OIDC RP-Initiated Logout
        app.MapGet("/auth/logout", (IConfiguration config) =>
        {
            var corsOrigin = config["CORS_ORIGIN"] ?? "http://localhost:3000";
            return Results.SignOut(
                new AuthenticationProperties { RedirectUri = corsOrigin },
                ["OpenIdConnect", "Cookies"]);
        }).AllowAnonymous();

        // Authenticated — returns current user's profile (includes email + profileImageUrl)
        app.MapGet("/api/users/me", [Authorize] async (HttpContext ctx, IUserRepository users) =>
        {
            var userId = ctx.User.GetInternalUserId();
            if (userId is null)
                return Results.Unauthorized();

            var user = await users.GetByIdAsync(userId.Value, ctx.RequestAborted);
            if (user is null)
                return Results.NotFound();

            var email = ctx.User.FindFirstValue("email");
            var profileImageUrl = AvatarUrl(user.Id, user.ProfileImagePath);

            return Results.Ok(new
            {
                id               = user.Id,
                displayName      = user.DisplayName,
                avatarUrl        = user.AvatarUrl,
                email,
                profileImageUrl,
                cropX            = user.CropX,
                cropY            = user.CropY,
                cropZoom         = user.CropZoom,
                createdAt        = user.CreatedAt,
                onboardingStatus = user.OnboardingStatus switch
                {
                    OnboardingStatus.RemindLater => "remind_later",
                    OnboardingStatus.Dismissed   => "dismissed",
                    _                            => "not_started",
                },
            });
        });

        // Authenticated — update current user's display name
        app.MapPut("/api/users/me", [Authorize] async (HttpContext ctx, IUserRepository users, IEventBus eventBus) =>
        {
            var userId = ctx.User.GetInternalUserId();
            if (userId is null)
                return Results.Unauthorized();

            var body = await ctx.Request.ReadFromJsonAsync<UpdateDisplayNameRequest>(ctx.RequestAborted);
            if (body is null)
                return Results.BadRequest();

            var displayName = body.DisplayName?.Trim();
            if (string.IsNullOrEmpty(displayName) || displayName.Length < 1 || displayName.Length > 50)
                return Results.BadRequest(new { error = "Display name must be 1–50 characters." });

            await users.UpdateDisplayNameAsync(userId.Value, displayName, ctx.RequestAborted);

            var user = await users.GetByIdAsync(userId.Value, ctx.RequestAborted);
            var profileImageUrl = AvatarUrl(userId.Value, user?.ProfileImagePath);

            await eventBus.PublishAsync(new UserProfileUpdatedIntegrationEvent
            {
                UserId          = userId.Value,
                DisplayName     = displayName,
                ProfileImageUrl = profileImageUrl,
            }, ctx.RequestAborted);

            return Results.Ok(new { displayName });
        });

        // Authenticated — upload/replace avatar
        app.MapPut("/api/users/me/avatar", async (
            HttpContext ctx,
            IUserRepository users,
            IEventBus eventBus,
            IFileStorageService fileStorage,
            IConfiguration config) =>
        {
            var userId = ctx.User.GetInternalUserId();
            if (userId is null)
                return Results.Unauthorized();

            // Enforce 10 MB limit before buffering the request body (prevents memory/disk exhaustion)
            var bodySizeFeature = ctx.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (bodySizeFeature is { IsReadOnly: false })
                bodySizeFeature.MaxRequestBodySize = 10 * 1024 * 1024;

            if (!ctx.Request.HasFormContentType)
                return Results.BadRequest(new { error = "Expected multipart/form-data." });

            var form = await ctx.Request.ReadFormAsync(ctx.RequestAborted);
            var file = form.Files["file"];
            if (file is null)
                return Results.BadRequest(new { error = "No file provided." });

            // Fast fail on declared content type (client-supplied, not authoritative)
            var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp",
                "image/heic", "image/heif",
            };
            if (!allowedTypes.Contains(file.ContentType))
                return Results.BadRequest(new { error = "Unsupported file type. Allowed: JPEG, PNG, GIF, WebP, HEIC." });

            // Secondary size guard in case Content-Length was absent or bypassed
            if (file.Length > 10 * 1024 * 1024)
                return Results.BadRequest(new { error = "File exceeds 10 MB limit." });

            // Validate magic bytes — verify the file is actually an image regardless of declared content type
            await using var stream = file.OpenReadStream();
            var header = new byte[12];
            var bytesRead = await stream.ReadAsync(header, ctx.RequestAborted);
            if (bytesRead < 3 || !HasValidImageMagicBytes(header))
                return Results.BadRequest(new { error = "File does not appear to be a valid image." });
            stream.Position = 0;

            // Parse crop values — use InvariantCulture to handle any locale
            if (!float.TryParse(form["cropX"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cropX) ||
                !float.TryParse(form["cropY"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cropY) ||
                !float.TryParse(form["cropZoom"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cropZoom))
                return Results.BadRequest(new { error = "Invalid crop values." });

            // Delete existing avatar if present
            var existing = await users.GetByIdAsync(userId.Value, ctx.RequestAborted);
            if (existing?.ProfileImagePath != null)
                await fileStorage.DeleteAsync(existing.ProfileImagePath, ctx.RequestAborted);

            // Save new avatar
            var saved = await fileStorage.SaveAsync(stream, file.FileName, ctx.RequestAborted);

            await users.UpdateAvatarAsync(userId.Value, saved.RelativePath, cropX, cropY, cropZoom, ctx.RequestAborted);

            var profileImageUrl = AvatarUrl(userId.Value, saved.RelativePath);

            await eventBus.PublishAsync(new UserProfileUpdatedIntegrationEvent
            {
                UserId          = userId.Value,
                DisplayName     = existing?.DisplayName ?? string.Empty,
                ProfileImageUrl = profileImageUrl,
            }, ctx.RequestAborted);

            return Results.Ok(new { profileImageUrl });
        }).RequireAuthorization();

        // Authenticated — delete avatar
        app.MapDelete("/api/users/me/avatar", [Authorize] async (
            HttpContext ctx,
            IUserRepository users,
            IEventBus eventBus,
            IFileStorageService fileStorage) =>
        {
            var userId = ctx.User.GetInternalUserId();
            if (userId is null)
                return Results.Unauthorized();

            var user = await users.GetByIdAsync(userId.Value, ctx.RequestAborted);
            if (user?.ProfileImagePath != null)
                await fileStorage.DeleteAsync(user.ProfileImagePath, ctx.RequestAborted);

            await users.DeleteAvatarAsync(userId.Value, ctx.RequestAborted);

            await eventBus.PublishAsync(new UserProfileUpdatedIntegrationEvent
            {
                UserId          = userId.Value,
                DisplayName     = user?.DisplayName ?? string.Empty,
                ProfileImageUrl = null,
            }, ctx.RequestAborted);

            return Results.NoContent();
        });

        // Authenticated — serve user's avatar image
        app.MapGet("/api/users/{userId:guid}/avatar", [Authorize] async (
            Guid userId,
            IUserRepository users,
            IConfiguration config,
            HttpContext ctx) =>
        {
            var user = await users.GetByIdAsync(userId, ctx.RequestAborted);
            if (user?.ProfileImagePath is null)
                return Results.NotFound();

            var uploadPath = config["UPLOAD_PATH"] ?? "/uploads";
            var fullPath = Path.GetFullPath(Path.Combine(uploadPath, user.ProfileImagePath));

            // Path traversal guard
            if (!fullPath.StartsWith(Path.GetFullPath(uploadPath) + Path.DirectorySeparatorChar))
                return Results.BadRequest();

            if (!File.Exists(fullPath))
                return Results.NotFound();

            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            var contentType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png"            => "image/png",
                ".gif"            => "image/gif",
                ".webp"           => "image/webp",
                _                 => "application/octet-stream",
            };

            ctx.Response.Headers.CacheControl = "private, max-age=86400";
            var stream = File.OpenRead(fullPath);
            return Results.File(stream, contentType, enableRangeProcessing: false);
        });

        // Authenticated — set current user's onboarding wizard status
        app.MapPut("/api/users/me/onboarding", [Authorize] async (HttpContext ctx, IUserRepository users) =>
        {
            var userId = ctx.User.GetInternalUserId();
            if (userId is null)
                return Results.Unauthorized();

            var body = await ctx.Request.ReadFromJsonAsync<SetOnboardingStatusRequest>(ctx.RequestAborted);
            var status = body?.Status switch
            {
                "dismissed"    => (OnboardingStatus?)OnboardingStatus.Dismissed,
                "remind_later" => (OnboardingStatus?)OnboardingStatus.RemindLater,
                _              => null,
            };
            if (status is null)
                return Results.BadRequest(new { error = "Status must be 'dismissed' or 'remind_later'." });

            await users.SetOnboardingStatusAsync(userId.Value, status.Value, ctx.RequestAborted);
            return Results.NoContent();
        });

        // Authenticated — returns all users with online status; supports ?q= name filter
        app.MapGet("/api/users", [Authorize] async (HttpContext ctx, IUserRepository users,
            IPresenceService presence, string? q) =>
        {
            var currentUserId = ctx.User.GetInternalUserId();
            if (currentUserId is null)
                return Results.Unauthorized();

            var allUsers = await users.GetAllAsync(q, ctx.RequestAborted);

            var result = new List<object>(allUsers.Count);
            foreach (var user in allUsers)
            {
                if (user.Id == currentUserId.Value) continue;

                var isOnline = await presence.IsOnlineAsync(user.Id, ctx.RequestAborted);
                var profileImageUrl = AvatarUrl(user.Id, user.ProfileImagePath);
                result.Add(new
                {
                    id             = user.Id,
                    displayName    = user.DisplayName,
                    avatarUrl      = user.AvatarUrl,
                    profileImageUrl,
                    isOnline,
                });
            }

            return Results.Ok(result);
        });

        return app;
    }

    private sealed record UpdateDisplayNameRequest(string? DisplayName);
    private sealed record SetOnboardingStatusRequest(string? Status);
}
