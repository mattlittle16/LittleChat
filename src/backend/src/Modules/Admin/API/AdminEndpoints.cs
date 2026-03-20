using LittleChat.Modules.Admin.Application.Commands;
using LittleChat.Modules.Admin.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts;

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
    }
}

public sealed record BanRequest(int BanDurationHours);
public sealed record TopicMemberRequest(Guid UserId);
