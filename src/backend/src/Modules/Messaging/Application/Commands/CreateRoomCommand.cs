using MediatR;
using Messaging.Domain;

namespace Messaging.Application.Commands;

public sealed record CreateRoomCommand(
    string Name,
    Guid CreatedBy,
    bool IsPrivate = false,
    IReadOnlyList<Guid>? InvitedUserIds = null
) : IRequest<Room>;
