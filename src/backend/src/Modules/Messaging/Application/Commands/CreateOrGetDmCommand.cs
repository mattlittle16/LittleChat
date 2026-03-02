using MediatR;
using Messaging.Domain;

namespace Messaging.Application.Commands;

public sealed record CreateOrGetDmCommand(Guid RequestingUserId, Guid TargetUserId) : IRequest<(Room Room, bool IsNew)>;
