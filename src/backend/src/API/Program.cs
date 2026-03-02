using Files.API;
using Identity.API;
using Messaging.API;
using Messaging.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Notifications.Infrastructure;
using Presence.API;
using Reactions.API;
using RealTime.API;
using RealTime.Infrastructure;
using Search.API;
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

// ── JWT Bearer Authentication ────────────────────────────────────────────────
var authority = builder.Configuration["AUTHENTIK_AUTHORITY"]
    ?? throw new InvalidOperationException("AUTHENTIK_AUTHORITY is required.");
var clientId = builder.Configuration["AUTHENTIK_CLIENT_ID"]
    ?? throw new InvalidOperationException("AUTHENTIK_CLIENT_ID is required.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            OnTokenValidated = async context =>
            {
                // Wired in T030: calls IUserSyncService once Identity module is implemented
                var userSync = context.HttpContext.RequestServices
                    .GetService<Identity.Application.Interfaces.IUserSyncService>();
                if (userSync is not null)
                    await userSync.EnsureUserExistsAsync(context.Principal!, context.HttpContext.RequestAborted);
            },
        };
    });

builder.Services.AddAuthorization();

// ── Valkey / StackExchange.Redis ──────────────────────────────────────────────
var valkeyConnectionString = builder.Configuration["VALKEY_CONNECTION_STRING"] ?? "valkey:6379";

// T018a — shared IConnectionMultiplexer (used by PresenceService + ChatHub.Heartbeat)
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
// SubClaimUserIdProvider lives in RealTime.Infrastructure — registered at composition root
builder.Services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();

builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddMessagingModule(builder.Configuration);
builder.Services.AddMessagingInfrastructure(builder.Configuration);
builder.Services.AddPresenceModule();
builder.Services.AddReactionsModule();
builder.Services.AddSearchModule();
builder.Services.AddFilesModule(builder.Configuration);
builder.Services.AddNotificationsModule();
builder.Services.AddRealTimeModule();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapIdentityEndpoints();
app.MapMessagingEndpoints();
app.MapSearchEndpoints();
app.MapFilesEndpoints();
app.MapHub<ChatHub>("/hubs/chat").RequireAuthorization();

app.Run();
