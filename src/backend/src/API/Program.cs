using System.Security.Claims;
using API;
using API.Services;
using Files.API;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Identity.API;
using Identity.Infrastructure;
using Messaging.API;
using Messaging.Application.Handlers;
using RealTime.Application.Handlers;
using Messaging.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Notifications.API;
using Notifications.Application.Handlers;
using Notifications.Infrastructure;
using Presence.API;
using Presence.Infrastructure;
using Reactions.API;
using RealTime.API;
using RealTime.Infrastructure;
using Search.API;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;
using Shared.Infrastructure;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── Shared Infrastructure ────────────────────────────────────────────────────
builder.Services.AddSharedInfrastructure();

// ── CORS ─────────────────────────────────────────────────────────────────────
var corsOrigin = builder.Configuration["CORS_ORIGIN"] ?? "http://localhost:3000";
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .WithOrigins(corsOrigin)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Content-Type", "Authorization", "X-Requested-With")
            .AllowCredentials()); // required for SignalR
});

// ── Authentication ────────────────────────────────────────────────────────────
var authority = builder.Configuration["AUTHENTIK_AUTHORITY"]
    ?? throw new InvalidOperationException("AUTHENTIK_AUTHORITY is required.");
var clientId = builder.Configuration["AUTHENTIK_CLIENT_ID"]
    ?? throw new InvalidOperationException("AUTHENTIK_CLIENT_ID is required.");
var clientSecret = builder.Configuration["AUTHENTIK_CLIENT_SECRET"]
    ?? throw new InvalidOperationException("AUTHENTIK_CLIENT_SECRET is required.");

builder.Services
    .AddAuthentication(options =>
    {
        // Default to JWT Bearer for API calls; OIDC scheme handles browser redirect flow
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = clientId;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "preferred_username",
            RoleClaimType = "groups",
        };

        // Allow JWT via query string for SignalR WebSocket connections
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var log = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                log.LogWarning("JWT_AUTH_FAILED: {Error}", context.Exception?.Message ?? "null");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var log = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                log.LogWarning("JWT_CHALLENGE: error={Error} description={Description} authResult={AuthResult}",
                    context.Error ?? "none",
                    context.ErrorDescription ?? "none",
                    context.AuthenticateFailure?.Message ?? "none");
                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
            // T030 — User sync + first-login event on every validated API call
            OnTokenValidated = async context =>
            {
                var userSync = context.HttpContext.RequestServices
                    .GetService<Identity.Application.Interfaces.IUserSyncService>();
                if (userSync is null) return;

                var (isNew, userId) = await userSync.EnsureUserExistsAsync(
                    context.Principal!, context.HttpContext.RequestAborted);

                // Stamp the internal UUID onto the principal so all endpoints can read it
                // without re-parsing the identity provider's non-UUID sub claim.
                ((ClaimsIdentity)context.Principal!.Identity!)
                    .AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

                if (isNew)
                {
                    var eventBus = context.HttpContext.RequestServices
                        .GetRequiredService<IEventBus>();
                    await eventBus.PublishAsync(new UserFirstLoginIntegrationEvent
                    {
                        UserId = userId,
                        DisplayName = context.Principal!.FindFirst("preferred_username")?.Value ?? "Unknown",
                        AvatarUrl = context.Principal.FindFirst("picture")?.Value,
                    }, context.HttpContext.RequestAborted);
                }
            },
        };
    })
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = authority;
        options.ClientId = clientId;
        options.ClientSecret = clientSecret;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.CallbackPath = "/auth/oidc-callback";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.MapInboundClaims = false;

        options.ProtocolValidator = new AuthentikProtocolValidator();

        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                context.ProtocolMessage.RedirectUri = $"{corsOrigin}/auth/oidc-callback";
                return Task.CompletedTask;
            },
            OnTokenResponseReceived = context =>
            {
                var accessToken = context.TokenEndpointResponse.AccessToken;
                context.Response.Redirect($"{corsOrigin}/auth/callback?access_token={Uri.EscapeDataString(accessToken)}");
                context.HandleResponse();
                return Task.CompletedTask;
            },
            OnRemoteFailure = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                logger.LogError(context.Failure, "OIDC remote failure: {Message}", context.Failure?.Message);
                context.Response.Redirect($"{corsOrigin}/?auth_error={Uri.EscapeDataString(context.Failure?.Message ?? "unknown")}");
                context.HandleResponse();
                return Task.CompletedTask;
            },
        };
    })
    .AddCookie();

builder.Services.AddAuthorization();

// ── Npgsql DataSource (shared, used by Identity.Infrastructure.UserRepository) ──
var pgConnectionString = builder.Configuration["POSTGRES_CONNECTION_STRING"]
    ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is required.");
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(pgConnectionString));

// ── Valkey / StackExchange.Redis ──────────────────────────────────────────────
var valkeyConnectionString = builder.Configuration["VALKEY_CONNECTION_STRING"] ?? "valkey:6379";

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(valkeyConnectionString));

// T018 — SignalR with Valkey backplane (Constitution Principle V: AbortOnConnectFail = false)
builder.Services
    .AddSignalR()
    .AddStackExchangeRedis(valkeyConnectionString, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("LittleChat");
        options.Configuration.AbortOnConnectFail = false;
    });

// ── Klipy HttpClient ──────────────────────────────────────────────────────────
var klipyApiKey = builder.Configuration["Klipy:ApiKey"]
    ?? builder.Configuration["KLIPY_API_KEY"]
    ?? string.Empty;
builder.Services.AddHttpClient("Klipy", client =>
{
    client.BaseAddress = new Uri("https://api.klipy.com/v2/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ── Problem Details / RFC 7807 ────────────────────────────────────────────────
builder.Services.AddHostedService<MessageCleanupService>();

builder.Services.AddProblemDetails();

// ── Module Registrations ──────────────────────────────────────────────────────
builder.Services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();

// Identity
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddIdentityInfrastructure();

// Messaging
builder.Services.AddMessagingModule(builder.Configuration);
builder.Services.AddMessagingInfrastructure(builder.Configuration);

// RealTime event handlers (registered at composition root)
builder.Services.AddScoped<IIntegrationEventHandler<UserFirstLoginIntegrationEvent>, UserFirstLoginHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<MessageSentIntegrationEvent>, MessageSentHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<ReactionUpdatedIntegrationEvent>, ReactionChangedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<MessageEditedIntegrationEvent>, MessageEditedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<MessageDeletedIntegrationEvent>, MessageDeletedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<MentionDetectedIntegrationEvent>, UserMentionedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<TopicAlertIntegrationEvent>, TopicAlertHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<DmMessageSentIntegrationEvent>, DmUnreadNotificationHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<DmCreatedIntegrationEvent>, DmCreatedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<DmDeletedIntegrationEvent>, DmDeletedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<RoomDeletedIntegrationEvent>, RoomDeletedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<MemberAddedIntegrationEvent>, MemberAddedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<MemberRemovedIntegrationEvent>, MemberRemovedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<UserProfileUpdatedIntegrationEvent>, UserProfileUpdatedHandler>();

// Other modules
builder.Services.AddPresenceModule();
builder.Services.AddPresenceInfrastructure();
builder.Services.AddReactionsModule();
builder.Services.AddSearchModule();
builder.Services.AddFilesModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddNotificationsApiModule();
builder.Services.AddRealTimeModule();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Event Bus ─────────────────────────────────────────────────────────────────
// Handler discovery is driven by DI registrations — no explicit Subscribe calls needed.

// Run all pending EF Core migrations on startup (idempotent — safe in every environment)
{
    using var scope = app.Services.CreateScope();
    var messagingDb = scope.ServiceProvider.GetRequiredService<Messaging.Infrastructure.Persistence.LittleChatDbContext>();
    await messagingDb.Database.MigrateAsync();
    var notificationsDb = scope.ServiceProvider.GetRequiredService<Notifications.Infrastructure.NotificationsDbContext>();
    await notificationsDb.Database.MigrateAsync();

    // Seed the General room if it doesn't exist yet (idempotent — checks is_protected flag)
    var generalRoom = messagingDb.Rooms
        .FirstOrDefault(r => r.IsProtected && !r.IsDm);
    if (generalRoom is null)
    {
        var newGeneral = new Messaging.Infrastructure.Persistence.Entities.RoomEntity
        {
            Id          = Guid.NewGuid(),
            Name        = "General",
            IsDm        = false,
            Visibility  = "public",
            IsProtected = true,
            CreatedBy   = null,
            OwnerId     = null,
            CreatedAt   = DateTime.UtcNow,
        };
        messagingDb.Rooms.Add(newGeneral);
        await messagingDb.SaveChangesAsync();
        generalRoom = newGeneral;
    }

    // Backfill: ensure every existing user is a member of General (idempotent)
    var existingMemberIds = messagingDb.RoomMemberships
        .Where(m => m.RoomId == generalRoom.Id)
        .Select(m => m.UserId)
        .ToHashSet();

    var allUserIds = messagingDb.Users
        .Select(u => u.Id)
        .ToList();

    var toAdd = allUserIds
        .Where(id => !existingMemberIds.Contains(id))
        .Select(id => new Messaging.Infrastructure.Persistence.Entities.RoomMembershipEntity
        {
            UserId     = id,
            RoomId     = generalRoom.Id,
            LastReadAt = DateTime.UtcNow,
            JoinedAt   = DateTime.UtcNow,
        })
        .ToList();

    if (toAdd.Count > 0)
    {
        messagingDb.RoomMemberships.AddRange(toAdd);
        await messagingDb.SaveChangesAsync();
    }
}

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors();

// ── Request diagnostics ───────────────────────────────────────────────────────
app.Use(async (ctx, next) =>
{
    var log = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
    if (ctx.Request.Path.StartsWithSegments("/auth/callback"))
        log.LogWarning("AUTH_CALLBACK method={Method} query={Query}",
            ctx.Request.Method, ctx.Request.QueryString);

    if (ctx.Request.Path.StartsWithSegments("/api"))
    {
        var auth = ctx.Request.Headers.Authorization.ToString();
        log.LogWarning("API_REQUEST path={Path} auth={Auth}",
            ctx.Request.Path,
            string.IsNullOrEmpty(auth) ? "MISSING" : $"Bearer ...{auth[^10..]}");
    }
    await next(ctx);
});

app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapIdentityEndpoints();
app.MapMessagingEndpoints();
app.MapReactionsEndpoints();
app.MapSearchEndpoints();
app.MapFilesEndpoints();
app.MapNotificationsEndpoints();
app.MapGifEndpoints();
app.MapVideoTokenEndpoints();
app.MapHub<ChatHub>("/hubs/chat").RequireAuthorization();

// Presence state is NOT cleared on startup. Clean deploys (graceful shutdown) drain all
// SignalR connections via OnDisconnectedAsync before the process exits, so refcounts reach 0
// naturally. After a crash, clients auto-reconnect and call ReassertPresence() via the hub,
// which resets their refcount to 1 without wiping other users' state.
app.Run();

// Authentik includes id_token alongside the authorization code in its redirect (non-standard
// hybrid-flow behavior). The auth-response validator rejects this when response_mode is query.
// We override it to skip auth-response validation when a code is present — the id_token from
// the authorization endpoint is unused; valid tokens are obtained from the token endpoint.
sealed class AuthentikProtocolValidator : OpenIdConnectProtocolValidator
{
    public override void ValidateAuthenticationResponse(
        OpenIdConnectProtocolValidationContext validationContext)
    {
        // Skip auth-response validation entirely. State/CSRF is validated before this is called.
        // Authentik's auth response doesn't conform (returns tokens via query string).
        // The token endpoint exchange (ValidateTokenResponse) still runs normally.
    }
}
