import { create } from 'zustand'
import type { BookmarkFolder, Bookmark } from '../types'

interface BookmarkState {
  folders: BookmarkFolder[]
  unfiled: Bookmark[]
  setBookmarks: (folders: BookmarkFolder[], unfiled: Bookmark[]) => void
  addBookmark: (bookmark: Bookmark) => void
  removeBookmark: (bookmarkId: string) => void
  addFolder: (folder: BookmarkFolder) => void
  removeFolder: (folderId: string) => void
  moveBookmark: (bookmarkId: string, folderId: string | null) => void
}

export const useBookmarkStore = create<BookmarkState>((set) => ({
  folders: [],
  unfiled: [],
  setBookmarks: (folders, unfiled) => set({ folders, unfiled }),
  addBookmark: (bookmark) =>
    set((s) => ({
      unfiled: bookmark.folderId
        ? s.unfiled
        : [bookmark, ...s.unfiled],
      folders: bookmark.folderId
        ? s.folders.map(f =>
            f.id === bookmark.folderId
              ? { ...f, bookmarks: [bookmark, ...f.bookmarks] }
              : f
          )
        : s.folders,
    })),
  removeBookmark: (bookmarkId) =>
    set((s) => ({
      unfiled: s.unfiled.filter(b => b.id !== bookmarkId),
      folders: s.folders.map(f => ({
        ...f,
        bookmarks: f.bookmarks.filter(b => b.id !== bookmarkId),
      })),
    })),
  addFolder: (folder) => set((s) => ({ folders: [...s.folders, folder] })),
  removeFolder: (folderId) =>
    set((s) => {
      const moved = s.folders.find(f => f.id === folderId)?.bookmarks ?? []
      return {
        folders: s.folders.filter(f => f.id !== folderId),
        unfiled: [...moved.map(b => ({ ...b, folderId: null })), ...s.unfiled],
      }
    }),
  moveBookmark: (bookmarkId, folderId) =>
    set((s) => {
      // Find the bookmark
      const inUnfiled = s.unfiled.find(b => b.id === bookmarkId)
      const inFolder = s.folders.flatMap(f => f.bookmarks).find(b => b.id === bookmarkId)
      const bm = inUnfiled ?? inFolder
      if (!bm) return s

      const updated = { ...bm, folderId }
      return {
        unfiled: folderId
          ? s.unfiled.filter(b => b.id !== bookmarkId)
          : [updated, ...s.unfiled.filter(b => b.id !== bookmarkId)],
        folders: s.folders.map(f => {
          if (f.id === folderId) return { ...f, bookmarks: [updated, ...f.bookmarks] }
          return { ...f, bookmarks: f.bookmarks.filter(b => b.id !== bookmarkId) }
        }),
      }
    }),
}))
