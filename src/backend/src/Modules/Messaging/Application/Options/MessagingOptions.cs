namespace Messaging.Application.Options;

public sealed class MessagingOptions
{
    /// <summary>
    /// Default number of messages returned per page (GET /api/rooms/{id}/messages).
    /// Configured via MESSAGE_PAGE_SIZE environment variable. Defaults to 100.
    /// </summary>
    public int MessagePageSize { get; set; } = 100;
}
