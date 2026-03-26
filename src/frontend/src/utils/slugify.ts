import type { Room } from '../types'

export function slugify(text: string): string {
  return text
    .toLowerCase()
    .trim()
    .replace(/\s+/g, '-')
    .replace(/[^a-z0-9-]/g, '')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '')
}

export function roomPath(room: Room): string {
  if (room.isDm) return `/dm/${slugify(room.otherUserDisplayName ?? room.name)}`
  return `/topics/${slugify(room.name)}`
}

export function findRoomBySlug(rooms: Room[], slug: string, isDm: boolean): Room | undefined {
  return rooms.filter(r => r.isDm === isDm).find(r => {
    const s = isDm ? slugify(r.otherUserDisplayName ?? r.name) : slugify(r.name)
    return s === slug
  })
}
