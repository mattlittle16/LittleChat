using Npgsql;
using Reactions.Application;

namespace Reactions.Infrastructure;

public sealed class ReactionRepository : IReactionRepository
{
    private readonly NpgsqlDataSource _db;

    public ReactionRepository(NpgsqlDataSource db)
    {
        _db = db;
    }

    public async Task<(bool Added, int Count, IReadOnlyList<string> Users)> ToggleAsync(
        Guid messageId,
        Guid userId,
        string emoji,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Step 1: try to delete existing reaction (toggle off)
        await using var deleteCmd = new NpgsqlCommand(
            "DELETE FROM reactions WHERE message_id = $1 AND user_id = $2 AND emoji = $3",
            conn, tx);
        deleteCmd.Parameters.AddWithValue(messageId);
        deleteCmd.Parameters.AddWithValue(userId);
        deleteCmd.Parameters.AddWithValue(emoji);

        var deleted = await deleteCmd.ExecuteNonQueryAsync(ct);
        var added = deleted == 0;

        if (added)
        {
            // Step 2: insert new reaction
            await using var insertCmd = new NpgsqlCommand(
                "INSERT INTO reactions (id, message_id, user_id, emoji) VALUES (gen_random_uuid(), $1, $2, $3) ON CONFLICT DO NOTHING",
                conn, tx);
            insertCmd.Parameters.AddWithValue(messageId);
            insertCmd.Parameters.AddWithValue(userId);
            insertCmd.Parameters.AddWithValue(emoji);
            await insertCmd.ExecuteNonQueryAsync(ct);
        }

        // Step 3: get remaining count and display names for this emoji on this message
        await using var countCmd = new NpgsqlCommand(
            """
            SELECT u.display_name
            FROM reactions r
            JOIN users u ON u.id = r.user_id
            WHERE r.message_id = $1 AND r.emoji = $2
            ORDER BY r.id
            """,
            conn, tx);
        countCmd.Parameters.AddWithValue(messageId);
        countCmd.Parameters.AddWithValue(emoji);

        var users = new List<string>();
        await using var reader = await countCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            users.Add(reader.GetString(0));

        await tx.CommitAsync(ct);
        return (added, users.Count, users);
    }
}
