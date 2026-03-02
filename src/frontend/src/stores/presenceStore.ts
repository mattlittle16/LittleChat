import { create } from 'zustand';

interface PresenceState {
  onlineUsers: Set<string>;
  setOnline: (userId: string) => void;
  setOffline: (userId: string) => void;
  setInitialPresence: (onlineUserIds: string[]) => void;
  isOnline: (userId: string) => boolean;
}

export const usePresenceStore = create<PresenceState>((set, get) => ({
  onlineUsers: new Set(),

  setOnline: (userId) =>
    set((state) => ({ onlineUsers: new Set(state.onlineUsers).add(userId) })),

  setOffline: (userId) =>
    set((state) => {
      const next = new Set(state.onlineUsers);
      next.delete(userId);
      return { onlineUsers: next };
    }),

  setInitialPresence: (onlineUserIds) =>
    set({ onlineUsers: new Set(onlineUserIds) }),

  isOnline: (userId) => get().onlineUsers.has(userId),
}));
