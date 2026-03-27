using System.Security.Claims;
using System.Threading.RateLimiting;
using API;
using API.Services;
using Microsoft.AspNetCore.RateLimiting;
using Files.API;
using LittleChat.Modules.Admin.API;
using LittleChat.Modules.Admin.Application.Commands;
using LittleChat.Modules.Admin.Infrastructure;
using LittleChat.Modules.Admin.Infrastructure.Middleware;
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
using EnrichedMessaging.API;
using EnrichedMessaging.Infrastructure;
using EnrichedMessaging.Application.Handlers;

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
var authority = builder.Configuration["OIDC_AUTHORITY"]
    ?? throw new InvalidOperationException("OIDC_AUTHORITY is required.");
var clientId = builder.Configuration["OIDC_CLIENT_ID"]
    ?? throw new InvalidOperationException("OIDC_CLIENT_ID is required.");
var clientSecret = builder.Configuration["OIDC_CLIENT_SECRET"]
    ?? throw new InvalidOperationException("OIDC_CLIENT_SECRET is required.");
var skipResponseValidation = builder.Configuration["OIDC_SKIP_RESPONSE_VALIDATION"] == "true";

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

        if (skipResponseValidation)
            options.ProtocolValidator = new OidcSkipResponseValidator();

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
builder.Services.AddMemoryCache();

// ── Rate Limiting ─────────────────────────────────────────────────────────────
var rlMessages    = builder.Configuration.GetValue("RateLimit:MessagesPerMinute",  60);
var rlSearch      = builder.Configuration.GetValue("RateLimit:SearchPerMinute",    10);
var rlGifSearch   = builder.Configuration.GetValue("RateLimit:GifSearchPerMinute", 15);
var rlRooms       = builder.Configuration.GetValue("RateLimit:RoomsPerMinute",      5);

builder.Services.AddSingleton(new SignalRRateLimiter(rlMessages));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static string PartitionKey(HttpContext ctx, string policy)
    {
        var userId = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? ctx.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        return $"{userId}:{policy}";
    }

    SlidingWindowRateLimiterOptions Sliding(int limit) => new()
    {
        PermitLimit = limit,
        Window = TimeSpan.FromMinutes(1),
        SegmentsPerWindow = 6,
        QueueLimit = 0,
        AutoReplenishment = true,
    };

    options.AddPolicy("messages",   ctx => RateLimitPartition.GetSlidingWindowLimiter(PartitionKey(ctx, "messages"),   _ => Sliding(rlMessages)));
    options.AddPolicy("search",     ctx => RateLimitPartition.GetSlidingWindowLimiter(PartitionKey(ctx, "search"),     _ => Sliding(rlSearch)));
    options.AddPolicy("gif-search", ctx => RateLimitPartition.GetSlidingWindowLimiter(PartitionKey(ctx, "gif-search"), _ => Sliding(rlGifSearch)));
    options.AddPolicy("rooms",      ctx => RateLimitPartition.GetSlidingWindowLimiter(PartitionKey(ctx, "rooms"),      _ => Sliding(rlRooms)));
});

// ── Npgsql DataSource (shared, used by Identity.Infrastructure.UserRepository) ──
var pgConnectionString = builder.Configuration["POSTGRES_CONNECTION_STRING"]
    ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is required.");
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(pgConnectionString));

// ── Valkey / StackExchange.Redis ──────────────────────────────────────────────
var valkeyConnectionString = builder.Configuration["VALKEY_CONNECTION_STRING"] ?? "valkey:6379";

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(valkeyConnectionString));

// ── Health Checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgSql(pgConnectionString, name: "postgres", tags: ["ready"])
    .AddRedis(valkeyConnectionString, name: "redis", tags: ["ready"]);

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
builder.Services.AddScoped<IIntegrationEventHandler<ReactionUpdatedIntegrationEvent>, ReactionAddedNotificationHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<MessageEditedIntegrationEvent>, MessageEditedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<MessageDeletedIntegrationEvent>, MessageDeletedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<MessageDeletedIntegrationEvent>, MessageDeletedPollCleanupHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<MentionDetectedIntegrationEvent>, UserMentionedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<TopicAlertIntegrationEvent>, TopicAlertHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<DmMessageSentIntegrationEvent>, DmUnreadNotificationHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<DmCreatedIntegrationEvent>, DmCreatedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<DmDeletedIntegrationEvent>, DmDeletedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<RoomDeletedIntegrationEvent>, RoomDeletedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<MemberAddedIntegrationEvent>, MemberAddedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<MemberRemovedIntegrationEvent>, MemberRemovedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<UserProfileUpdatedIntegrationEvent>, UserProfileUpdatedHandler>();

// EnrichedMessaging / US1 quote notification
builder.Services.AddScoped<IIntegrationEventHandler<MessageQuotedIntegrationEvent>, QuoteNotificationHandler>();
// US2 poll vote
builder.Services.AddScoped<IIntegrationEventHandler<PollVoteUpdatedIntegrationEvent>, PollVoteUpdatedHandler>();
// US3 highlight
builder.Services.AddScoped<IIntegrationEventHandler<HighlightChangedIntegrationEvent>, HighlightChangedHandler>();
// US5 user status
builder.Services.AddScoped<IIntegrationEventHandler<UserStatusUpdatedIntegrationEvent>, UserStatusUpdatedHandler>();
// US7 link preview
builder.Services.AddScoped<IIntegrationEventHandler<LinkPreviewReadyIntegrationEvent>, LinkPreviewReadyHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<MessageSentIntegrationEvent>, MessageSentLinkPreviewHandler>();

// Admin module
builder.Services.AddAdminModule(builder.Configuration);
builder.Services.AddScoped<IIntegrationEventHandler<UserForceLoggedOutIntegrationEvent>, UserForceLoggedOutHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<UserBannedIntegrationEvent>, UserBannedMessageHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<UserUnbannedIntegrationEvent>, UserUnbannedMessageHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<AdminAddedTopicMemberIntegrationEvent>, AdminAddedTopicMemberMessageHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<AdminRemovedTopicMemberIntegrationEvent>, AdminRemovedTopicMemberMessageHandler>();

// Other modules
builder.Services.AddPresenceModule();
builder.Services.AddPresenceInfrastructure();
builder.Services.AddReactionsModule();
builder.Services.AddSearchModule();
builder.Services.AddFilesModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddNotificationsApiModule();
builder.Services.AddEnrichedMessagingModule(builder.Configuration);
builder.Services.AddEnrichedMessagingApiModule();
builder.Services.AddRealTimeModule();
builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();

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
    var adminDb = scope.ServiceProvider.GetRequiredService<LittleChat.Modules.Admin.Infrastructure.AdminDbContext>();
    await adminDb.Database.MigrateAsync();
    var enrichedDb = scope.ServiceProvider.GetRequiredService<EnrichedMessaging.Infrastructure.EnrichedMessagingDbContext>();
    await enrichedDb.Database.MigrateAsync();

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

// ── Request diagnostics (dev only) ────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
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
}

app.UseAuthentication();
app.UseTokenBlocklist();
app.UseAuthorization();
app.UseRateLimiter();

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
app.MapAdminEndpoints();
app.MapEnrichedMessagingEndpoints();
app.MapHealthChecks("/health");
app.MapHealthChecks("/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

// Clear all presence state on startup so stale connection Sets from a previous run or crash
// don't cause users to appear stuck online. Clients re-establish their presence as they
// reconnect and fire OnConnectedAsync.
{
    using var scope = app.Services.CreateScope();
    var presence = scope.ServiceProvider.GetRequiredService<IPresenceService>();
    await presence.ClearAllAsync();
}

app.Run();

// Some IdPs (e.g. Authentik) use a hybrid flow that returns tokens in the authorization redirect
// (non-standard OIDC behaviour). The auth-response validator rejects this when response_mode is
// query. Set OIDC_SKIP_RESPONSE_VALIDATION=true to opt in to skipping that check.
// Standard code-flow providers (Keycloak, Auth0, Okta, Authelia) don't need this.
sealed class OidcSkipResponseValidator : OpenIdConnectProtocolValidator
{
    public override void ValidateAuthenticationResponse(
        OpenIdConnectProtocolValidationContext validationContext)
    {
        // No-op: skips auth-response validation for IdPs that use hybrid flow.
        // State/CSRF is validated before this is called.
        // The token endpoint exchange (ValidateTokenResponse) still runs normally.
    }
}
