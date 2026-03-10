namespace Messaging.Infrastructure.Persistence.Entities;

public sealed class MessageAttachmentEntity
{
    public Guid   Id           { get; set; }
    public Guid   MessageId    { get; set; }
    public string FileName     { get; set; } = string.Empty;
    public long   FileSize     { get; set; }
    public string FilePath     { get; set; } = string.Empty;
    public string ContentType  { get; set; } = "application/octet-stream";
    public bool   IsImage      { get; set; }
    public int    DisplayOrder { get; set; }

    public MessageEntity Message { get; set; } = null!;
}
