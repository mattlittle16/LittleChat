using Microsoft.Extensions.Configuration;
using Npgsql;
using Shared.Contracts.DTOs;
using Shared.Contracts.Interfaces;

namespace EnrichedMessaging.Infrastructure.Services;

public sealed class PollDataReaderService : IPollDataReader
{
    private readonly string _connectionString;

    public PollDataReaderService(IConfiguration configuration)
    {
        _connectionString = configuration["POSTGRES_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is required.");
    }

    public async Task<IReadOnlyDictionary<Guid, PollDataDto>> GetForMessagesAsync(
        IReadOnlyList<Guid> messageIds, Guid currentUserId, CancellationToken ct = default)
    {
        if (messageIds.Count == 0)
            return new Dictionary<Guid, PollDataDto>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT p.id AS PollId, p.message_id AS MessageId, p.question AS Question,
                   p.vote_mode AS VoteMode,
                   po.id AS OptionId, po.text AS OptionText, po.display_order AS DisplayOrder,
                   COUNT(pv.id) AS VoteCount,
                   STRING_AGG(pv.display_name, ',' ORDER BY pv.created_at) AS VoterNames
            FROM polls p
            JOIN poll_options po ON po.poll_id = p.id
            LEFT JOIN poll_votes pv ON pv.option_id = po.id
            WHERE p.message_id = ANY(@ids)
            GROUP BY p.id, po.id
            ORDER BY p.id, po.display_order";

        var pollMap = new Dictionary<Guid, (Guid MessageId, string Question, string VoteMode, List<(Guid Id, string Text, int Order, int Count, string? Names)> Options)>();

        var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("ids", messageIds.ToArray());

        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var pollId   = reader.GetGuid(reader.GetOrdinal("PollId"));
                var msgId    = reader.GetGuid(reader.GetOrdinal("MessageId"));
                var question = reader.GetString(reader.GetOrdinal("Question"));
                var voteMode = reader.GetString(reader.GetOrdinal("VoteMode"));
                var oid      = reader.GetGuid(reader.GetOrdinal("OptionId"));
                var otext    = reader.GetString(reader.GetOrdinal("OptionText"));
                var oorder   = reader.GetInt32(reader.GetOrdinal("DisplayOrder"));
                var ocount   = (int)(long)reader.GetValue(reader.GetOrdinal("VoteCount"));
                var onames   = reader.IsDBNull(reader.GetOrdinal("VoterNames")) ? null : reader.GetString(reader.GetOrdinal("VoterNames"));

                if (!pollMap.ContainsKey(pollId))
                    pollMap[pollId] = (msgId, question, voteMode, new List<(Guid, string, int, int, string?)>());
                pollMap[pollId].Options.Add((oid, otext, oorder, ocount, onames));
            }
        }

        if (pollMap.Count == 0)
            return new Dictionary<Guid, PollDataDto>();

        // Fetch current user's voted option IDs for all loaded polls
        var pollIds = pollMap.Keys.ToArray();
        var votedCmd = new NpgsqlCommand(
            "SELECT option_id FROM poll_votes WHERE poll_id = ANY(@pids) AND user_id = @uid", conn);
        votedCmd.Parameters.AddWithValue("pids", pollIds);
        votedCmd.Parameters.AddWithValue("uid", currentUserId);

        var votedOptionIds = new HashSet<Guid>();
        await using (var votedReader = await votedCmd.ExecuteReaderAsync(ct))
        {
            while (await votedReader.ReadAsync(ct))
                votedOptionIds.Add(votedReader.GetGuid(0));
        }

        var result = new Dictionary<Guid, PollDataDto>();
        foreach (var (pollId, (msgId, question, voteMode, options)) in pollMap)
        {
            var optionDtos = options
                .Select(o => new PollOptionDto(
                    o.Id, o.Text, o.Order, o.Count,
                    o.Names?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? []))
                .ToList();

            var userVotedIds = optionDtos
                .Where(o => votedOptionIds.Contains(o.OptionId))
                .Select(o => o.OptionId)
                .ToList();

            result[msgId] = new PollDataDto(pollId, question, voteMode, optionDtos, userVotedIds);
        }

        return result;
    }
}
