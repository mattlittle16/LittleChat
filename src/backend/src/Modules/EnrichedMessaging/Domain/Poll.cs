namespace EnrichedMessaging.Domain;

public sealed record Poll(
    Guid Id,
    Guid MessageId,
    string Question,
    string VoteMode,
    IReadOnlyList<PollOption> Options,
    DateTime CreatedAt
);

public sealed record PollOption(
    Guid Id,
    Guid PollId,
    string Text,
    int DisplayOrder,
    int VoteCount,
    IReadOnlyList<string> VoterDisplayNames
);
