using MediatR;

namespace Messaging.Application.Commands;

public sealed record TransferOwnershipCommand(
    Guid RoomId,
    Guid CurrentOwnerUserId,
    string CurrentOwnerDisplayName,
    Guid NewOwnerUserId,
    string NewOwnerDisplayName
) : IRequest;
