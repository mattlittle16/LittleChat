namespace Messaging.Application.Commands;

public sealed record FileUpload(
    Stream Stream,
    string FileName,
    long   FileSize
);
