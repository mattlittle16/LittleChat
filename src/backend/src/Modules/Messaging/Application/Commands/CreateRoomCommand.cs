using MediatR;
using Messaging.Domain;

namespace Messaging.Application.Commands;

public sealed record CreateRoomCommand(string Name, Guid CreatedBy) : IRequest<Room>;
