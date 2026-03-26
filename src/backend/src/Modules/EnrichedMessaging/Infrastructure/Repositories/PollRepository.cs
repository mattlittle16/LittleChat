using EnrichedMessaging.Domain;
using EnrichedMessaging.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EnrichedMessaging.Infrastructure.Repositories;

public sealed class PollRepository : IPollRepository
{
    private readonly EnrichedMessagingDbContext _db;
    private readonly string _connectionString;

    public PollRepository(EnrichedMessagingDbContext db, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _db = db;
        _connectionString = configuration["POSTGRES_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is required.");
    }

    public async Task<Poll?> GetByIdAsync(Guid pollId, Guid currentUserId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return await LoadPollById(conn, pollId, currentUserId, ct);
    }

    public async Task<Poll?> GetByMessageIdAsync(Guid messageId, Guid currentUserId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return await LoadPollByMessageId(conn, messageId, currentUserId, ct);
    }

    private static Task<Poll?> LoadPollById(NpgsqlConnection conn, Guid pollId, Guid currentUserId, CancellationToken ct)
    {
        const string sql = @"
            SELECT p.id AS PollId, p.message_id AS MessageId, p.question AS Question,
                   p.vote_mode AS VoteMode, p.created_at AS CreatedAt,
                   po.id AS OptionId, po.text AS OptionText, po.display_order AS DisplayOrder,
                   COUNT(pv.id) AS VoteCount
            FROM polls p
            JOIN poll_options po ON po.poll_id = p.id
            LEFT JOIN poll_votes pv ON pv.option_id = po.id
            WHERE p.id = @pollId
            GROUP BY p.id, po.id
            ORDER BY po.display_order";

        var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("pollId", pollId);
        return LoadPollFromCommand(cmd, conn, currentUserId, ct);
    }

    private static Task<Poll?> LoadPollByMessageId(NpgsqlConnection conn, Guid messageId, Guid currentUserId, CancellationToken ct)
    {
        const string sql = @"
            SELECT p.id AS PollId, p.message_id AS MessageId, p.question AS Question,
                   p.vote_mode AS VoteMode, p.created_at AS CreatedAt,
                   po.id AS OptionId, po.text AS OptionText, po.display_order AS DisplayOrder,
                   COUNT(pv.id) AS VoteCount
            FROM polls p
            JOIN poll_options po ON po.poll_id = p.id
            LEFT JOIN poll_votes pv ON pv.option_id = po.id
            WHERE p.message_id = @messageId
            GROUP BY p.id, po.id
            ORDER BY po.display_order";

        var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("messageId", messageId);
        return LoadPollFromCommand(cmd, conn, currentUserId, ct);
    }

    private static async Task<Poll?> LoadPollFromCommand(NpgsqlCommand cmd, NpgsqlConnection conn, Guid currentUserId, CancellationToken ct)
    {

        Guid? pollId = null;
        string? question = null, voteMode = null;
        DateTime createdAt = default;
        Guid? messageId = null;
        var options = new List<(Guid id, string text, int order, int count)>();

        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                if (pollId is null)
                {
                    pollId    = reader.GetGuid(reader.GetOrdinal("PollId"));
                    messageId = reader.GetGuid(reader.GetOrdinal("MessageId"));
                    question  = reader.GetString(reader.GetOrdinal("Question"));
                    voteMode  = reader.GetString(reader.GetOrdinal("VoteMode"));
                    createdAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"));
                }
                options.Add((
                    reader.GetGuid(reader.GetOrdinal("OptionId")),
                    reader.GetString(reader.GetOrdinal("OptionText")),
                    reader.GetInt32(reader.GetOrdinal("DisplayOrder")),
                    (int)(long)reader.GetValue(reader.GetOrdinal("VoteCount"))
                ));
            }
        }

        if (pollId is null) return null;

        // Fetch voter display names per option as proper rows — avoids comma-delimiter corruption
        var voterNamesByOption = new Dictionary<Guid, List<string>>();
        var voterNamesCmd = new NpgsqlCommand(
            "SELECT option_id, display_name FROM poll_votes WHERE poll_id = @pid ORDER BY created_at", conn);
        voterNamesCmd.Parameters.AddWithValue("pid", pollId.Value);
        await using (var voterReader = await voterNamesCmd.ExecuteReaderAsync(ct))
        {
            while (await voterReader.ReadAsync(ct))
            {
                var optId = voterReader.GetGuid(0);
                var name  = voterReader.GetString(1);
                if (!voterNamesByOption.TryGetValue(optId, out var list))
                    voterNamesByOption[optId] = list = [];
                list.Add(name);
            }
        }

        // Get current user's voted option IDs
        var votedCmd = new NpgsqlCommand(
            "SELECT option_id FROM poll_votes WHERE poll_id = @pid AND user_id = @uid", conn);
        votedCmd.Parameters.AddWithValue("pid", pollId.Value);
        votedCmd.Parameters.AddWithValue("uid", currentUserId);
        var votedIds = new List<Guid>();
        await using (var votedReader = await votedCmd.ExecuteReaderAsync(ct))
        {
            while (await votedReader.ReadAsync(ct))
                votedIds.Add(votedReader.GetGuid(0));
        }

        var pollOptions = options.Select(o => new PollOption(
            o.id,
            pollId.Value,
            o.text,
            o.order,
            o.count,
            voterNamesByOption.TryGetValue(o.id, out var names) ? names : []
        )).ToList();

        return new Poll(pollId.Value, messageId!.Value, question!, voteMode!, pollOptions, createdAt, votedIds);
    }

    public async Task CreatePollMessageAsync(Guid messageId, Guid roomId, Guid userId, string question, DateTime now, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new NpgsqlCommand(@"
            INSERT INTO messages (id, room_id, user_id, content, message_type, created_at, expires_at, is_system)
            VALUES (@id, @rid, @uid, @content, 'poll', @now, @expires, false)", conn);
        cmd.Parameters.AddWithValue("id", messageId);
        cmd.Parameters.AddWithValue("rid", roomId);
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("content", question);
        cmd.Parameters.AddWithValue("now", now);
        cmd.Parameters.AddWithValue("expires", now.AddDays(30));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task CreateAsync(Poll poll, IReadOnlyList<PollOption> options, CancellationToken ct = default)
    {
        var pollEntity = new PollEntity
        {
            Id = poll.Id,
            MessageId = poll.MessageId,
            Question = poll.Question,
            VoteMode = poll.VoteMode,
            CreatedAt = poll.CreatedAt,
        };
        _db.Polls.Add(pollEntity);

        foreach (var opt in options)
        {
            _db.PollOptions.Add(new PollOptionEntity
            {
                Id = opt.Id,
                PollId = poll.Id,
                Text = opt.Text,
                DisplayOrder = opt.DisplayOrder,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<Poll?> AddVoteAsync(Guid pollId, Guid optionId, Guid userId, string displayName, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Determine vote mode
        var modeCmd = new NpgsqlCommand("SELECT vote_mode FROM polls WHERE id = @id", conn);
        modeCmd.Parameters.AddWithValue("id", pollId);
        var mode = (string?)await modeCmd.ExecuteScalarAsync(ct);
        if (mode is null) return null;

        if (mode == "single")
        {
            // Check if already voted for this exact option (toggle off)
            var existsCmd = new NpgsqlCommand(
                "SELECT id FROM poll_votes WHERE poll_id = @pid AND option_id = @oid AND user_id = @uid", conn);
            existsCmd.Parameters.AddWithValue("pid", pollId);
            existsCmd.Parameters.AddWithValue("oid", optionId);
            existsCmd.Parameters.AddWithValue("uid", userId);
            var existingId = await existsCmd.ExecuteScalarAsync(ct);

            if (existingId is not null)
            {
                // Toggle off: delete this vote
                var delCmd = new NpgsqlCommand(
                    "DELETE FROM poll_votes WHERE poll_id = @pid AND option_id = @oid AND user_id = @uid", conn);
                delCmd.Parameters.AddWithValue("pid", pollId);
                delCmd.Parameters.AddWithValue("oid", optionId);
                delCmd.Parameters.AddWithValue("uid", userId);
                await delCmd.ExecuteNonQueryAsync(ct);
            }
            else
            {
                // Remove all prior votes for this user on the poll, then insert new
                var delAllCmd = new NpgsqlCommand(
                    "DELETE FROM poll_votes WHERE poll_id = @pid AND user_id = @uid", conn);
                delAllCmd.Parameters.AddWithValue("pid", pollId);
                delAllCmd.Parameters.AddWithValue("uid", userId);
                await delAllCmd.ExecuteNonQueryAsync(ct);

                var insCmd = new NpgsqlCommand(
                    "INSERT INTO poll_votes (id, poll_id, option_id, user_id, display_name, created_at) VALUES (@id, @pid, @oid, @uid, @dn, NOW())", conn);
                insCmd.Parameters.AddWithValue("id", Guid.NewGuid());
                insCmd.Parameters.AddWithValue("pid", pollId);
                insCmd.Parameters.AddWithValue("oid", optionId);
                insCmd.Parameters.AddWithValue("uid", userId);
                insCmd.Parameters.AddWithValue("dn", displayName);
                await insCmd.ExecuteNonQueryAsync(ct);
            }
        }
        else
        {
            // Multi-vote: toggle the specific option
            var existsCmd = new NpgsqlCommand(
                "SELECT id FROM poll_votes WHERE poll_id = @pid AND option_id = @oid AND user_id = @uid", conn);
            existsCmd.Parameters.AddWithValue("pid", pollId);
            existsCmd.Parameters.AddWithValue("oid", optionId);
            existsCmd.Parameters.AddWithValue("uid", userId);
            var existingId = await existsCmd.ExecuteScalarAsync(ct);

            if (existingId is not null)
            {
                var delCmd = new NpgsqlCommand(
                    "DELETE FROM poll_votes WHERE poll_id = @pid AND option_id = @oid AND user_id = @uid", conn);
                delCmd.Parameters.AddWithValue("pid", pollId);
                delCmd.Parameters.AddWithValue("oid", optionId);
                delCmd.Parameters.AddWithValue("uid", userId);
                await delCmd.ExecuteNonQueryAsync(ct);
            }
            else
            {
                var insCmd = new NpgsqlCommand(
                    "INSERT INTO poll_votes (id, poll_id, option_id, user_id, display_name, created_at) VALUES (@id, @pid, @oid, @uid, @dn, NOW())" +
                    " ON CONFLICT (poll_id, option_id, user_id) DO NOTHING", conn);
                insCmd.Parameters.AddWithValue("id", Guid.NewGuid());
                insCmd.Parameters.AddWithValue("pid", pollId);
                insCmd.Parameters.AddWithValue("oid", optionId);
                insCmd.Parameters.AddWithValue("uid", userId);
                insCmd.Parameters.AddWithValue("dn", displayName);
                await insCmd.ExecuteNonQueryAsync(ct);
            }
        }

        return await LoadPollById(conn, pollId, userId, ct);
    }

    public async Task<bool> IsMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new NpgsqlCommand(
            "SELECT 1 FROM room_memberships WHERE room_id = @rid AND user_id = @uid LIMIT 1", conn);
        cmd.Parameters.AddWithValue("rid", roomId);
        cmd.Parameters.AddWithValue("uid", userId);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    public async Task<Guid?> GetRoomIdByPollIdAsync(Guid pollId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new NpgsqlCommand(
            "SELECT m.room_id FROM polls p JOIN messages m ON m.id = p.message_id WHERE p.id = @pid", conn);
        cmd.Parameters.AddWithValue("pid", pollId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    public async Task DeleteByMessageIdAsync(Guid messageId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = new NpgsqlCommand("DELETE FROM polls WHERE message_id = @mid", conn);
        cmd.Parameters.AddWithValue("mid", messageId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
