using MediatR;

namespace Messaging.Application.Commands;

public sealed record RemoveMemberCommand(Guid RoomId, Guid OwnerUserId, Guid TargetUserId) : IRequest;
