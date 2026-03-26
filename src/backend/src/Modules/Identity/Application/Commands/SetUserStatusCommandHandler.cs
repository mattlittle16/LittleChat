using Identity.Domain;
using MediatR;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;

namespace Identity.Application.Commands;

public sealed class SetUserStatusCommandHandler : IRequestHandler<SetUserStatusCommand, SetUserStatusResult>
{
    private static readonly HashSet<string> AllowedColors = new(StringComparer.OrdinalIgnoreCase)
        { "green", "yellow", "red", "grey", "blue", "orange", "purple", "pink" };

    private readonly IUserRepository _users;
    private readonly IEventBus _eventBus;

    public SetUserStatusCommandHandler(IUserRepository users, IEventBus eventBus)
    {
        _users = users;
        _eventBus = eventBus;
    }

    public async Task<SetUserStatusResult> Handle(SetUserStatusCommand request, CancellationToken cancellationToken)
    {
        if (request.Text is not null && request.Text.Length > 60)
            throw new InvalidOperationException("Status text must be 60 characters or fewer.");

        if (request.Color is not null && !AllowedColors.Contains(request.Color))
            throw new InvalidOperationException($"Invalid color key. Allowed: {string.Join(", ", AllowedColors)}.");

        await _users.UpdateStatusAsync(request.UserId, request.Emoji, request.Text, request.Color, cancellationToken);

        await _eventBus.PublishAsync(new UserStatusUpdatedIntegrationEvent
        {
            UserId = request.UserId,
            Emoji  = request.Emoji,
            Text   = request.Text,
            Color  = request.Color,
        }, cancellationToken);

        return new SetUserStatusResult(request.Emoji, request.Text, request.Color);
    }
}
