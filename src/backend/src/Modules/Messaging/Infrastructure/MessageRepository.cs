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
            Id        = message.Id,
            RoomId    = message.RoomId,
            UserId    = message.UserId,
            Content   = message.Content,
            CreatedAt = message.CreatedAt,
            ExpiresAt = message.ExpiresAt,
        };

        // Add attachment entities
        for (int i = 0; i < message.Attachments.Count; i++)
        {
            var att = message.Attachments[i];
            entity.Attachments.Add(new MessageAttachmentEntity
            {
                Id           = att.Id,
                MessageId    = att.MessageId,
                FileName     = att.FileName,
                FileSize     = att.FileSize,
                FilePath     = att.FilePath,
                ContentType  = att.ContentType,
                IsImage      = att.IsImage,
                DisplayOrder = att.DisplayOrder,
            });
        }

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
            .Include(m => m.Attachments)
            .Include(m => m.Reactions).ThenInclude(r => r.User)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        return entity is null ? null : ToMessage(entity);
    }

    public async Task<MessagePage> GetPageAsync(
        Guid roomId, DateTime? before, Guid? beforeId, int limit, CancellationToken ct = default)
    {
        IQueryable<MessageEntity> query = _db.Messages
            .Include(m => m.User)
            .Include(m => m.Attachments)
            .Include(m => m.Reactions).ThenInclude(r => r.User)
            .Where(m => m.RoomId == roomId);

        // Keyset pagination
        if (before.HasValue && beforeId.HasValue)
        {
            var ts  = before.Value;
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
        var page    = messages.Take(limit).Select(ToMessage).ToList();
        page.Reverse();

        return new MessagePage(page, hasMore);
    }

    public async Task<MessagePage> GetPageAroundAsync(
        Guid roomId, Guid aroundId, int limit, CancellationToken ct = default)
    {
        var half = limit / 2;

        // Resolve the anchor's timestamp so we can do keyset queries
        var anchorTs = await _db.Messages
            .Where(m => m.Id == aroundId && m.RoomId == roomId)
            .Select(m => (DateTime?)m.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (anchorTs is null)
            return new MessagePage([], false, false);

        var ts  = anchorTs.Value;
        var aid = aroundId;

        // Older half (messages strictly before the anchor in sort order)
        var olderRaw = await _db.Messages
            .Include(m => m.User)
            .Include(m => m.Attachments)
            .Include(m => m.Reactions).ThenInclude(r => r.User)
            .Where(m => m.RoomId == roomId)
            .Where(m => m.CreatedAt < ts || (m.CreatedAt == ts && m.Id.CompareTo(aid) < 0))
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(half + 1)
            .ToListAsync(ct);

        var hasMore  = olderRaw.Count > half;
        var older    = olderRaw.Take(half).ToList();
        older.Reverse();

        // Anchor itself (with full includes)
        var anchor = await _db.Messages
            .Include(m => m.User)
            .Include(m => m.Attachments)
            .Include(m => m.Reactions).ThenInclude(r => r.User)
            .FirstOrDefaultAsync(m => m.Id == aroundId, ct);

        if (anchor is null)
            return new MessagePage([], false, false);

        // Newer half (messages strictly after the anchor in sort order)
        var newerRaw = await _db.Messages
            .Include(m => m.User)
            .Include(m => m.Attachments)
            .Include(m => m.Reactions).ThenInclude(r => r.User)
            .Where(m => m.RoomId == roomId)
            .Where(m => m.CreatedAt > ts || (m.CreatedAt == ts && m.Id.CompareTo(aid) > 0))
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Take(half + 1)
            .ToListAsync(ct);

        var hasNewer = newerRaw.Count > half;
        var newer    = newerRaw.Take(half).ToList();

        var combined = older.Concat([anchor]).Concat(newer).Select(ToMessage).ToList();
        return new MessagePage(combined, hasMore, hasNewer);
    }

    public async Task<MessagePage> GetPageAfterAsync(
        Guid roomId, DateTime after, Guid afterId, int limit, CancellationToken ct = default)
    {
        var messages = await _db.Messages
            .Include(m => m.User)
            .Include(m => m.Attachments)
            .Include(m => m.Reactions).ThenInclude(r => r.User)
            .Where(m => m.RoomId == roomId)
            .Where(m => m.CreatedAt > after || (m.CreatedAt == after && m.Id.CompareTo(afterId) > 0))
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasNewer = messages.Count > limit;
        var page     = messages.Take(limit).Select(ToMessage).ToList();
        return new MessagePage(page, HasMore: false, HasNewer: hasNewer);
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

        entity.Content  = newContent;
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
        Id:                e.Id,
        RoomId:            e.RoomId,
        UserId:            e.UserId,
        AuthorDisplayName: e.User?.DisplayName ?? string.Empty,
        AuthorAvatarUrl:   e.User?.AvatarUrl,
        Content:           e.Content,
        Attachments:       e.Attachments
            .OrderBy(a => a.DisplayOrder)
            .Select(a => new MessageAttachment(
                Id:           a.Id,
                MessageId:    a.MessageId,
                FileName:     a.FileName,
                FileSize:     a.FileSize,
                FilePath:     a.FilePath,
                ContentType:  a.ContentType,
                IsImage:      a.IsImage,
                DisplayOrder: a.DisplayOrder))
            .ToList(),
        CreatedAt:         e.CreatedAt,
        EditedAt:          e.EditedAt,
        ExpiresAt:         e.ExpiresAt,
        Reactions:         e.Reactions
            .OrderBy(r => r.CreatedAt)
            .Select(r => new MessageReaction(r.Emoji, r.User?.DisplayName ?? string.Empty))
            .ToList()
    );
}
