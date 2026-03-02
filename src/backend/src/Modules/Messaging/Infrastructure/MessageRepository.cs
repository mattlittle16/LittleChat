using Messaging.Domain;
using Messaging.Infrastructure.Persistence;
using Messaging.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Messaging.Infrastructure;

public sealed class MessageRepository : IMessageRepository
{
    private readonly LittleChatDbContext _db;

    public MessageRepository(LittleChatDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> CreateAsync(Message message, CancellationToken ct = default)
    {
        var entity = new MessageEntity
        {
            Id = message.Id,
            RoomId = message.RoomId,
            UserId = message.UserId,
            Content = message.Content,
            FilePath = message.FilePath,
            FileName = message.FileName,
            FileSize = message.FileSize,
            CreatedAt = message.CreatedAt,
            ExpiresAt = message.ExpiresAt,
        };

        _db.Messages.Add(entity);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("duplicate key") == true ||
            ex.InnerException?.Message.Contains("23505") == true)
        {
            // Idempotent — client resent same MessageId; treat as success
            _db.ChangeTracker.Clear();
        }

        return entity.Id;
    }

    public async Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Messages
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        return entity is null ? null : ToMessage(entity);
    }

    public async Task<MessagePage> GetPageAsync(
        Guid roomId, DateTime? before, Guid? beforeId, int limit, CancellationToken ct = default)
    {
        IQueryable<MessageEntity> query = _db.Messages
            .Include(m => m.User)
            .Where(m => m.RoomId == roomId);

        // Keyset pagination: WHERE (created_at, id) < (before_ts, before_id)
        if (before.HasValue && beforeId.HasValue)
        {
            var ts = before.Value;
            var bid = beforeId.Value;
            query = query.Where(m =>
                m.CreatedAt < ts ||
                (m.CreatedAt == ts && m.Id.CompareTo(bid) < 0));
        }

        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasMore = messages.Count > limit;
        var page = messages.Take(limit).Select(ToMessage).ToList();
        page.Reverse(); // return chronological order

        return new MessagePage(page, hasMore);
    }

    public async Task<bool> IsMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        return await _db.RoomMemberships
            .AnyAsync(m => m.RoomId == roomId && m.UserId == userId, ct);
    }

    public async Task<string?> GetRoomNameAsync(Guid roomId, CancellationToken ct = default)
    {
        return await _db.Rooms
            .Where(r => r.Id == roomId)
            .Select(r => r.Name)
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpdateLastReadAtAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        var membership = await _db.RoomMemberships
            .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId, ct);

        if (membership is not null)
        {
            membership.LastReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<DateTime> EditAsync(Guid messageId, Guid userId, string newContent, CancellationToken ct = default)
    {
        var entity = await _db.Messages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (entity is null) throw new InvalidOperationException("Message not found.");
        if (entity.UserId != userId) throw new UnauthorizedAccessException("Cannot edit another user's message.");

        entity.Content = newContent;
        entity.EditedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return entity.EditedAt!.Value;
    }

    public async Task DeleteAsync(Guid messageId, Guid userId, CancellationToken ct = default)
    {
        var entity = await _db.Messages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (entity is null) throw new InvalidOperationException("Message not found.");
        if (entity.UserId != userId) throw new UnauthorizedAccessException("Cannot delete another user's message.");

        _db.Messages.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    private static Message ToMessage(MessageEntity e) => new(
        Id: e.Id,
        RoomId: e.RoomId,
        UserId: e.UserId,
        AuthorDisplayName: e.User?.DisplayName ?? string.Empty,
        AuthorAvatarUrl: e.User?.AvatarUrl,
        Content: e.Content,
        FilePath: e.FilePath,
        FileName: e.FileName,
        FileSize: e.FileSize,
        CreatedAt: e.CreatedAt,
        EditedAt: e.EditedAt,
        ExpiresAt: e.ExpiresAt
    );
}
