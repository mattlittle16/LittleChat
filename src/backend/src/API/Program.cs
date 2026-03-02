using Files.API;
using Npgsql;
using Identity.API;
using Identity.Infrastructure;
using Messaging.API;
using Messaging.Application.Handlers;
using RealTime.Application.Handlers;
using Messaging.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
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
            .AllowAnyMethod()
            .AllowAnyHeader()
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
        options.DefaultSignInScheme = OpenIdConnectDefaults.AuthenticationScheme;
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

                var isNew = await userSync.EnsureUserExistsAsync(
                    context.Principal!, context.HttpContext.RequestAborted);

                if (isNew)
                {
                    var sub = context.Principal!.FindFirst("sub")?.Value;
                    if (Guid.TryParse(sub, out var userId))
                    {
                        var eventBus = context.HttpContext.RequestServices
                            .GetRequiredService<IEventBus>();
                        await eventBus.PublishAsync(new UserFirstLoginIntegrationEvent
                        {
                            UserId = userId,
                            DisplayName = context.Principal.FindFirst("preferred_username")?.Value ?? "Unknown",
                            AvatarUrl = context.Principal.FindFirst("picture")?.Value,
                        }, context.HttpContext.RequestAborted);
                    }
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
        options.CallbackPath = "/auth/callback";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.MapInboundClaims = false;

        options.Events = new OpenIdConnectEvents
        {
            OnTokenResponseReceived = context =>
            {
                // Extract access token and redirect to frontend
                var accessToken = context.TokenEndpointResponse.AccessToken;
                context.Response.Redirect($"{corsOrigin}/auth/callback?access_token={Uri.EscapeDataString(accessToken)}");
                context.HandleResponse();
                return Task.CompletedTask;
            },
        };
    })
    .AddCookie(); // required as sign-in scheme for OpenIdConnect

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

// ── Problem Details / RFC 7807 ────────────────────────────────────────────────
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

// Other modules
builder.Services.AddPresenceModule();
builder.Services.AddPresenceInfrastructure();
builder.Services.AddReactionsModule();
builder.Services.AddSearchModule();
builder.Services.AddFilesModule(builder.Configuration);
builder.Services.AddNotificationsModule();
builder.Services.AddRealTimeModule();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Event Bus subscriptions ───────────────────────────────────────────────────
var eventBus = app.Services.GetRequiredService<IEventBus>();
eventBus.Subscribe<UserFirstLoginIntegrationEvent, UserFirstLoginHandler>();
eventBus.Subscribe<MessageSentIntegrationEvent, MessageSentHandler>();
eventBus.Subscribe<ReactionUpdatedIntegrationEvent, ReactionChangedHandler>();
eventBus.Subscribe<MessageEditedIntegrationEvent, MessageEditedHandler>();
eventBus.Subscribe<MessageDeletedIntegrationEvent, MessageDeletedHandler>();
eventBus.Subscribe<MentionDetectedIntegrationEvent, UserMentionedHandler>();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapIdentityEndpoints();
app.MapMessagingEndpoints();
app.MapReactionsEndpoints();
app.MapSearchEndpoints();
app.MapFilesEndpoints();
app.MapHub<ChatHub>("/hubs/chat").RequireAuthorization();

app.Run();
