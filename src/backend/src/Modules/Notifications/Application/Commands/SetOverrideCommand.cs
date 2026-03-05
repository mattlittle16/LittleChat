using MediatR;
using Notifications.Domain;

namespace Notifications.Application.Commands;

public sealed record SetOverrideCommand(Guid UserId, Guid RoomId, string Level) : IRequest;

public sealed class SetOverrideCommandHandler : IRequestHandler<SetOverrideCommand>
{
    private static readonly HashSet<string> ValidLevels = ["all_messages", "mentions_only", "muted"];
    private readonly IConversationOverrideRepository _repo;

    public SetOverrideCommandHandler(IConversationOverrideRepository repo)
    {
        _repo = repo;
    }

    public async Task Handle(SetOverrideCommand request, CancellationToken cancellationToken)
    {
        if (!ValidLevels.Contains(request.Level))
            throw new ArgumentException($"Invalid level: {request.Level}");

        await _repo.UpsertAsync(new ConversationNotificationOverride(request.UserId, request.RoomId, request.Level), cancellationToken);
    }
}
