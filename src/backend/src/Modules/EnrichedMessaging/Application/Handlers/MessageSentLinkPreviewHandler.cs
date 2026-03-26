using System.Text.RegularExpressions;
using EnrichedMessaging.Application.Services;
using EnrichedMessaging.Domain;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace EnrichedMessaging.Application.Handlers;

public sealed class MessageSentLinkPreviewHandler : IIntegrationEventHandler<MessageSentIntegrationEvent>
{
    private static readonly Regex UrlRegex = new(
        @"(?<![`])https?://[^\s`]+(?![`])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Strips fenced code blocks (```...```) before inline spans to avoid unfurling URLs inside them
    private static readonly Regex FencedCodeRegex = new(@"```[\s\S]*?```", RegexOptions.Compiled);
    private static readonly Regex CodeSpanRegex   = new(@"`[^`]+`", RegexOptions.Compiled);

    private readonly ILinkPreviewFetcher _fetcher;
    private readonly IServiceScopeFactory _scopeFactory;

    public MessageSentLinkPreviewHandler(
        ILinkPreviewFetcher fetcher,
        IServiceScopeFactory scopeFactory)
    {
        _fetcher = fetcher;
        _scopeFactory = scopeFactory;
    }

    public Task HandleAsync(MessageSentIntegrationEvent evt, CancellationToken cancellationToken = default)
    {
        // Only text messages
        if (evt.MessageType != "text" || evt.IsSystem)
            return Task.CompletedTask;

        // Find first URL outside backtick code spans
        var stripped = CodeSpanRegex.Replace(evt.Content, string.Empty);
        var match = UrlRegex.Match(stripped);
        if (!match.Success)
            return Task.CompletedTask;

        var url = match.Value;

        // Fire-and-forget: don't block message delivery.
        // Create a fresh DI scope so scoped services (DbContext-backed repositories)
        // are not tied to the request scope that will be disposed before this task completes.
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _fetcher.FetchAsync(url, CancellationToken.None);
                if (result is null) return;

                var preview = new LinkPreview(
                    Guid.NewGuid(), evt.MessageId, result.Url,
                    result.Title, result.Description, result.ThumbnailUrl,
                    false, DateTime.UtcNow);

                await using var scope = _scopeFactory.CreateAsyncScope();
                var previews  = scope.ServiceProvider.GetRequiredService<ILinkPreviewRepository>();
                var eventBus  = scope.ServiceProvider.GetRequiredService<IEventBus>();

                await previews.UpsertAsync(preview, CancellationToken.None);

                await eventBus.PublishAsync(new LinkPreviewReadyIntegrationEvent
                {
                    MessageId    = evt.MessageId,
                    RoomId       = evt.RoomId,
                    Url          = result.Url,
                    Title        = result.Title,
                    Description  = result.Description,
                    ThumbnailUrl = result.ThumbnailUrl,
                    IsDismissed  = false,
                }, CancellationToken.None);
            }
            catch
            {
                // Silent failure per spec
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }
}
