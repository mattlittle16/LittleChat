using EnrichedMessaging.Application.Commands;
using EnrichedMessaging.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Contracts;

namespace EnrichedMessaging.API;

public static class EnrichedMessagingEndpoints
{
    public static IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder app)
    {
        // ── Polls ────────────────────────────────────────────────────────────────

        // POST /api/polls — create a poll message (roomId comes from body)
        app.MapPost("/api/polls",
            [Authorize] async (CreatePollBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var displayName = ctx.User.FindFirst("preferred_username")?.Value ?? "Unknown";
                var avatarUrl   = ctx.User.FindFirst("picture")?.Value;

                try
                {
                    var result = await sender.Send(new CreatePollCommand(
                        RoomId:      body.RoomId,
                        UserId:      userId.Value,
                        DisplayName: displayName,
                        AvatarUrl:   avatarUrl,
                        Question:    body.Question,
                        Options:     body.Options,
                        VoteMode:    body.VoteMode ?? "single"
                    ), ctx.RequestAborted);

                    return Results.Created(
                        $"/api/polls/{result.PollId}",
                        new { messageId = result.MessageId, pollId = result.PollId });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
            });

        // POST /api/polls/{pollId}/vote — cast or toggle a vote
        app.MapPost("/api/polls/{pollId:guid}/vote",
            [Authorize] async (Guid pollId, CastVoteBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var displayName = ctx.User.FindFirst("preferred_username")?.Value ?? "Unknown";

                try
                {
                    var result = await sender.Send(new CastPollVoteCommand(
                        PollId:      pollId,
                        OptionId:    body.OptionId,
                        UserId:      userId.Value,
                        DisplayName: displayName
                    ), ctx.RequestAborted);

                    return Results.Ok(new
                    {
                        pollId  = result.PollId,
                        options = result.Options,
                        currentUserVotedOptionIds = result.CurrentUserVotedOptionIds,
                    });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
            });

        // GET /api/polls/{pollId} — get poll state
        app.MapGet("/api/polls/{pollId:guid}",
            [Authorize] async (Guid pollId, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var result = await sender.Send(new GetPollQuery(pollId, userId.Value), ctx.RequestAborted);
                if (result is null)
                    return Results.NotFound();

                return Results.Ok(result);
            });

        // ── Highlights ───────────────────────────────────────────────────────────

        // GET /api/rooms/{roomId}/highlights
        app.MapGet("/api/rooms/{roomId:guid}/highlights",
            [Authorize] async (Guid roomId, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                try
                {
                    var result = await sender.Send(new GetHighlightsQuery(roomId, userId.Value), ctx.RequestAborted);
                    return Results.Ok(result);
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
            });

        // POST /api/rooms/{roomId}/highlights
        app.MapPost("/api/rooms/{roomId:guid}/highlights",
            [Authorize] async (Guid roomId, AddHighlightBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var displayName = ctx.User.FindFirst("preferred_username")?.Value ?? "Unknown";

                try
                {
                    var result = await sender.Send(
                        new AddHighlightCommand(roomId, body.MessageId, userId.Value, displayName),
                        ctx.RequestAborted);
                    return Results.Created($"/api/rooms/{roomId}/highlights/{result.Id}", result);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
            });

        // DELETE /api/rooms/{roomId}/highlights/{highlightId}
        app.MapDelete("/api/rooms/{roomId:guid}/highlights/{highlightId:guid}",
            [Authorize] async (Guid roomId, Guid highlightId, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                try
                {
                    await sender.Send(new RemoveHighlightCommand(roomId, highlightId, userId.Value), ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(ex.Message);
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
            });

        // ── Bookmarks ────────────────────────────────────────────────────────────

        // GET /api/bookmarks
        app.MapGet("/api/bookmarks",
            [Authorize] async (HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var result = await sender.Send(new GetBookmarksQuery(userId.Value), ctx.RequestAborted);
                return Results.Ok(result);
            });

        // POST /api/bookmarks
        app.MapPost("/api/bookmarks",
            [Authorize] async (AddBookmarkBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                try
                {
                    var result = await sender.Send(
                        new AddBookmarkCommand(userId.Value, body.MessageId, body.FolderId),
                        ctx.RequestAborted);
                    return Results.Created($"/api/bookmarks/{result!.Id}", result);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

        // DELETE /api/bookmarks/{bookmarkId}
        app.MapDelete("/api/bookmarks/{bookmarkId:guid}",
            [Authorize] async (Guid bookmarkId, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                try
                {
                    await sender.Send(new RemoveBookmarkCommand(bookmarkId, userId.Value), ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(ex.Message);
                }
            });

        // PATCH /api/bookmarks/{bookmarkId}
        app.MapPatch("/api/bookmarks/{bookmarkId:guid}",
            [Authorize] async (Guid bookmarkId, UpdateBookmarkBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                try
                {
                    await sender.Send(
                        new UpdateBookmarkFolderCommand(bookmarkId, userId.Value, body.FolderId),
                        ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

        // POST /api/bookmark-folders
        app.MapPost("/api/bookmark-folders",
            [Authorize] async (CreateFolderBody body, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                try
                {
                    var result = await sender.Send(
                        new CreateBookmarkFolderCommand(userId.Value, body.Name),
                        ctx.RequestAborted);
                    return Results.Created($"/api/bookmark-folders/{result.Id}", result);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

        // DELETE /api/bookmark-folders/{folderId}
        app.MapDelete("/api/bookmark-folders/{folderId:guid}",
            [Authorize] async (Guid folderId, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                try
                {
                    await sender.Send(new DeleteBookmarkFolderCommand(folderId, userId.Value), ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(ex.Message);
                }
            });

        // ── Daily Digest ─────────────────────────────────────────────────────────

        // GET /api/digest
        app.MapGet("/api/digest",
            [Authorize] async (HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var result = await sender.Send(new GetDailyDigestQuery(userId.Value), ctx.RequestAborted);
                return Results.Ok(result);
            });

        // ── Link Preview ─────────────────────────────────────────────────────────

        // DELETE /api/messages/{messageId}/link-preview
        app.MapDelete("/api/messages/{messageId:guid}/link-preview",
            [Authorize] async (Guid messageId, HttpContext ctx, ISender sender) =>
            {
                var userId = ctx.User.GetInternalUserId();
                if (userId is null)
                    return Results.Unauthorized();

                try
                {
                    await sender.Send(new DismissLinkPreviewCommand(messageId, userId.Value), ctx.RequestAborted);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(ex.Message);
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
            });

        return app;
    }
}

internal sealed record CreatePollBody(Guid RoomId, string Question, IReadOnlyList<string> Options, string? VoteMode);
internal sealed record CastVoteBody(Guid OptionId);
internal sealed record AddHighlightBody(Guid MessageId);
internal sealed record AddBookmarkBody(Guid MessageId, Guid? FolderId);
internal sealed record UpdateBookmarkBody(Guid? FolderId);
internal sealed record CreateFolderBody(string Name);
