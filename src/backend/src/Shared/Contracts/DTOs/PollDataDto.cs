namespace Shared.Contracts.DTOs;

public record PollDataDto(
    Guid PollId,
    string Question,
    string VoteMode,
    IReadOnlyList<PollOptionDto> Options,
    IReadOnlyList<Guid> CurrentUserVotedOptionIds
);

public record PollOptionDto(
    Guid OptionId,
    string Text,
    int DisplayOrder,
    int VoteCount,
    IReadOnlyList<string> VoterDisplayNames
);
