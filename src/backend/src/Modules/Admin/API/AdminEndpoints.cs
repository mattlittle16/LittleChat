using LittleChat.Modules.Admin.Application.Commands;
using LittleChat.Modules.Admin.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts;
using Shared.Contracts.Interfaces;

namespace LittleChat.Modules.Admin.API;

public static class AdminEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization(AdminModuleExtensions.PolicyName);

        group.MapGet("/users", async (IMediator mediator, string? q, int page = 1, int pageSize = 50) =>
        {
            var result = await mediator.Send(new GetUsersQuery(q, page, pageSize));
            return Results.Ok(result);
        });

        group.MapPost("/users/{userId:guid}/force-logout", async (
            Guid userId,
            HttpContext ctx,
            IMediator mediator,
            BanRequest body) =>
        {
            var adminId = ctx.User.GetInternalUserId();
            if (!adminId.HasValue) return Results.Unauthorized();

            var banHours = Math.Max(1, body.BanDurationHours);

            var adminName = ctx.User.FindFirst("preferred_username")?.Value
                ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                ?? "Unknown";

            var result = await mediator.Send(new ForceLogoutUserCommand(userId, adminId.Value, adminName, banHours));
            return result.Match(
                success => Results.Ok(new { userId = success.UserId, displayName = success.DisplayName, message = "Session terminated successfully." }),
                notFound => Results.NotFound(),
                selfLogout => Results.BadRequest(new { error = "Cannot force-logout yourself." }));
        });

        group.MapPost("/users/{userId:guid}/unban", async (
            Guid userId,
            HttpContext ctx,
            IMediator mediator) =>
        {
            var adminId = ctx.User.GetInternalUserId();
            if (!adminId.HasValue) return Results.Unauthorized();

            var adminName = ctx.User.FindFirst("preferred_username")?.Value
                ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                ?? "Unknown";

            var result = await mediator.Send(new UnbanUserCommand(userId, adminId.Value, adminName));
            return result.Match(
                success => Results.Ok(new { userId = success.UserId, displayName = success.DisplayName, message = "User unbanned successfully." }),
                notFound => Results.NotFound(),
                notBanned => Results.BadRequest(new { error = "User is not currently banned." }));
        });

        group.MapGet("/topics", async (IMediator mediator, string? q, int page = 1, int pageSize = 50) =>
        {
            var result = await mediator.Send(new GetTopicsQuery(q, page, pageSize));
            return Results.Ok(result);
        });

        group.MapPost("/topics", async (
            HttpContext ctx,
            IMediator mediator,
            CreateTopicRequest body) =>
        {
            var adminId = ctx.User.GetInternalUserId();
            if (!adminId.HasValue) return Results.Unauthorized();
            var adminName = ctx.User.FindFirst("preferred_username")?.Value
                ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                ?? "Unknown";

            var result = await mediator.Send(new AdminCreateTopicCommand(body.Name, adminId.Value, adminName));
            return result.Match(
                success     => Results.Ok(new { topicId = success.TopicId, name = success.Name }),
                invalidName => Results.BadRequest(new { error = "Topic name cannot be empty." }));
        });

        group.MapDelete("/topics/{topicId:guid}", async (
            Guid topicId,
            HttpContext ctx,
            IMediator mediator) =>
        {
            var adminId = ctx.User.GetInternalUserId();
            if (!adminId.HasValue) return Results.Unauthorized();
            var adminName = ctx.User.FindFirst("preferred_username")?.Value
                ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                ?? "Unknown";

            var result = await mediator.Send(new AdminDeleteTopicCommand(topicId, adminId.Value, adminName));
            return result.Match(
                success     => Results.Ok(new { topicId = success.TopicId, name = success.Name }),
                notFound    => Results.NotFound(new { error = "Topic not found." }),
                isProtected => Results.BadRequest(new { error = "Cannot delete a protected topic." }));
        });

        group.MapGet("/topics/{topicId:guid}/members", async (Guid topicId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetTopicMembersQuery(topicId));
            if (result is null) return Results.NotFound();
            return Results.Ok(result);
        });

        group.MapPost("/topics/{topicId:guid}/members", async (
            Guid topicId,
            HttpContext ctx,
            IMediator mediator,
            TopicMemberRequest body) =>
        {
            var adminId = ctx.User.GetInternalUserId();
            if (!adminId.HasValue) return Results.Unauthorized();
            var adminName = ctx.User.FindFirst("preferred_username")?.Value
                ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                ?? "Unknown";

            var result = await mediator.Send(new AdminAddTopicMemberCommand(topicId, body.UserId, adminId.Value, adminName));
            return result.Match(
                success      => Results.Ok(new { userId = success.UserId, displayName = success.DisplayName }),
                topicNotFound => Results.NotFound(new { error = "Topic not found." }),
                userNotFound  => Results.NotFound(new { error = "User not found." }),
                alreadyMember => Results.BadRequest(new { error = "User is already a member." }));
        });

        group.MapDelete("/topics/{topicId:guid}/members/{userId:guid}", async (
            Guid topicId,
            Guid userId,
            HttpContext ctx,
            IMediator mediator) =>
        {
            var adminId = ctx.User.GetInternalUserId();
            if (!adminId.HasValue) return Results.Unauthorized();
            var adminName = ctx.User.FindFirst("preferred_username")?.Value
                ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                ?? "Unknown";

            var result = await mediator.Send(new AdminRemoveTopicMemberCommand(topicId, userId, adminId.Value, adminName));
            return result.Match(
                success       => Results.Ok(new { userId = success.UserId, displayName = success.DisplayName }),
                topicNotFound => Results.NotFound(new { error = "Topic not found." }),
                userNotFound  => Results.NotFound(new { error = "User not found." }),
                notMember     => Results.BadRequest(new { error = "User is not a member." }));
        });

        group.MapGet("/audit-log", async (IMediator mediator, DateOnly? from, DateOnly? to, int page = 1, int pageSize = 50) =>
        {
            var result = await mediator.Send(new GetAuditLogQuery(from, to, page, pageSize));
            return Results.Ok(result);
        });

        group.MapPut("/users/{userId:guid}/display-name", async (
            Guid userId,
            HttpContext ctx,
            IMediator mediator,
            UpdateDisplayNameRequest body) =>
        {
            var adminId = ctx.User.GetInternalUserId();
            if (!adminId.HasValue) return Results.Unauthorized();
            var adminName = ctx.User.FindFirst("preferred_username")?.Value
                ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                ?? "Unknown";

            var result = await mediator.Send(new AdminUpdateDisplayNameCommand(userId, body.DisplayName ?? string.Empty, adminId.Value, adminName));
            return result.Match(
                success     => Results.Ok(new { userId = success.UserId, displayName = success.DisplayName }),
                userNotFound => Results.NotFound(new { error = "User not found." }),
                invalidName  => Results.BadRequest(new { error = "Display name must be 1–50 characters and cannot contain '@'." }));
        });

        group.MapDelete("/users/{userId:guid}/avatar", async (
            Guid userId,
            HttpContext ctx,
            IMediator mediator) =>
        {
            var adminId = ctx.User.GetInternalUserId();
            if (!adminId.HasValue) return Results.Unauthorized();
            var adminName = ctx.User.FindFirst("preferred_username")?.Value
                ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                ?? "Unknown";

            var result = await mediator.Send(new AdminRemoveAvatarCommand(userId, adminId.Value, adminName));
            return result.Match(
                success     => Results.NoContent(),
                userNotFound => Results.NotFound(new { error = "User not found." }));
        });

        group.MapPut("/users/{userId:guid}/avatar", async (
            Guid userId,
            HttpContext ctx,
            IMediator mediator,
            IFileStorageService fileStorage) =>
        {
            var adminId = ctx.User.GetInternalUserId();
            if (!adminId.HasValue) return Results.Unauthorized();

            var bodySizeFeature = ctx.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (bodySizeFeature is { IsReadOnly: false })
                bodySizeFeature.MaxRequestBodySize = 10 * 1024 * 1024;

            if (!ctx.Request.HasFormContentType)
                return Results.BadRequest(new { error = "Expected multipart/form-data." });

            var form = await ctx.Request.ReadFormAsync(ctx.RequestAborted);
            var file = form.Files["file"];
            if (file is null)
                return Results.BadRequest(new { error = "No file provided." });

            var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp",
                "image/heic", "image/heif",
            };
            if (!allowedTypes.Contains(file.ContentType))
                return Results.BadRequest(new { error = "Unsupported file type." });

            if (file.Length > 10 * 1024 * 1024)
                return Results.BadRequest(new { error = "File exceeds 10 MB limit." });

            var adminName = ctx.User.FindFirst("preferred_username")?.Value
                ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                ?? "Unknown";

            await using var stream = file.OpenReadStream();
            var result = await mediator.Send(new AdminUpdateAvatarCommand(userId, stream, file.FileName, adminId.Value, adminName));
            return result.Match(
                success     => Results.Ok(new { profileImageUrl = success.ProfileImageUrl }),
                userNotFound => Results.NotFound(new { error = "User not found." }),
                invalidFile  => Results.BadRequest(new { error = "File does not appear to be a valid image." }));
        });
    }
}

public sealed record BanRequest(int BanDurationHours);
public sealed record TopicMemberRequest(Guid UserId);
public sealed record CreateTopicRequest(string Name);
public sealed record UpdateDisplayNameRequest(string? DisplayName);
