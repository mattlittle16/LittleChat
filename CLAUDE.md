# LittleChat Development Guidelines

##MOST IMPORTANT 
Don't ever assume, guess, or make up stats or theories without investigating the code base first. If you do need to propose a theory, you MUST tell me it's a theory rather than propose it as fact.

##Important
Before you consider any tasks done where we are changing code I want you run the front build / lint process and make sure the backend compiles. 

Auto-generated from all feature plans. Last updated: 2026-03-01

## Active Technologies
- C# (.NET 8) — backend; TypeScript/React — frontend + ASP.NET Core SignalR (`@microsoft/signalr` v10), Zustand, `emoji-picker-react` v4.18, Npgsql (PostgreSQL), MediatR, Entity Framework Core (002-fix-dm-emoji-bugs)
- PostgreSQL (Npgsql raw SQL in repositories; EF Core for schema/migrations in Messaging module) (002-fix-dm-emoji-bugs)
- TypeScript 5.9 / React 19 + Tailwind CSS v4 (`@tailwindcss/vite`), shadcn/ui (cssVariables mode, slate base), Zustand v5, Vite 7, lucide-react (icon library already installed) (003-visual-redesign)
- `localStorage` — key `littlechat_theme` (`'light'` | `'dark'`) (003-visual-redesign)
- C# (.NET 8) — backend; TypeScript 5.9 / React 19 — frontend + ASP.NET Core SignalR, MediatR, Npgsql, Zustand v5, Tailwind CSS v4, shadcn/ui (slate/cssVariables), lucide-reac (004-user-content-controls)
- PostgreSQL via Npgsql raw SQL in repositories (004-user-content-controls)
- C# (.NET 8) — backend; TypeScript 5.9 / React 19 — frontend + ASP.NET Core SignalR, MediatR, Entity Framework Core (Npgsql), Zustand v5, Tailwind CSS v4, lucide-react, Web Notifications API (browser built-in) (005-notification-settings)
- PostgreSQL — two new tables in Notifications module (`NotificationsDbContext`); `room_memberships.last_read_at` (existing, Messaging module) (005-notification-settings)
- TypeScript 5.9 / React 19 (frontend only) + Tiptap v2 (`@tiptap/react`, `@tiptap/starter-kit`), existing `react-markdown` + `remark-gfm` unchanged (still used in chat history display) (006-inline-markdown-editor)
- No change — messages stored as raw markdown text in PostgreSQL; Tiptap doc serialized to markdown before send (006-inline-markdown-editor)
- TypeScript 5.9 / React 19 + Tiptap v2 (`@tiptap/react`, `@tiptap/starter-kit`), `react-markdown` + `remark-gfm`, Zustand v5, Tailwind CSS v4, shadcn/ui (slate/cssVariables), lucide-reac (007-chat-ux-fixes)
- N/A (frontend-only; reads existing Zustand store for message history and current user) (007-chat-ux-fixes)
- C# (.NET 8) — backend; TypeScript 5.9 / React 19 — frontend + ASP.NET Core minimal API, EF Core (Npgsql), MediatR, SignalR, Zustand v5, Tailwind CSS v4, shadcn/ui, SixLabors.ImageSharp 3.x + SixLabors.ImageSharp.Formats.Heic (new) (008-multi-file-attachments)
- PostgreSQL (EF Core for Messaging schema); local filesystem via `LocalFileStorageService` (UPLOAD_PATH volume) (008-multi-file-attachments)
- TypeScript 5.9 / React 19 (frontend only) + Vite 7 (`define` API), React hooks (`useEffect`, `useState`), Tailwind CSS v4, nginx alpine (009-update-detection)
- N/A — no persistence; all state is transient in-memory (009-update-detection)
- C# .NET 8 (backend) · TypeScript 5.9 / React 19 (frontend) + ASP.NET Core (SignalR, MediatR, EF Core), Tiptap v2, react-markdown + remark-gfm, Zustand v5, Tailwind CSS v4, shadcn/ui (010-chat-media-ux)
- PostgreSQL (Npgsql) — no schema changes for this feature (010-chat-media-ux)
- TypeScript 5.9 / React 19 + `emoji-picker-react` v4.18, `lucide-react` v0.576, Tailwind CSS v4, React DOM `createPortal` (011-emoji-reaction-ux)
- C# (.NET 8) backend; TypeScript 5.9 / React 19 frontend + ASP.NET Core minimal API, SignalR, MediatR, EF Core (Npgsql), Zustand v5, Tailwind CSS v4, shadcn/ui, lucide-reac (012-topics-overhaul)
- PostgreSQL — Messaging module EF Core schema (rooms, room_memberships, messages, new sidebar_groups) (012-topics-overhaul)
- C# (.NET 8) — backend; TypeScript 5.9 / React 19 — frontend + ASP.NET Core SignalR, MediatR, EF Core (Npgsql), Zustand v5, Tailwind CSS v4, shadcn/ui; **new**: `@dnd-kit/core` ^6.x, `@dnd-kit/sortable` ^8.x, `@dnd-kit/utilities` ^3.x (013-topic-dnd-membership)
- PostgreSQL via EF Core (Messaging module); one new column on `room_memberships` (013-topic-dnd-membership)
- C# (.NET 8) backend; TypeScript 5.9 / React 19 frontend + ASP.NET Core minimal API, SignalR, MediatR, Npgsql (raw SQL), EF Core (migrations only), Zustand v5, Tailwind CSS v4, shadcn/ui; **new frontend**: `react-easy-crop` (circular crop editor) (014-user-profile)
- PostgreSQL — new columns on `users` table; profile images on disk via `UPLOAD_PATH` env var (014-user-profile)
- C# .NET 8 (backend), TypeScript 5.9 / React 19 (frontend) + ASP.NET Core minimal API, MediatR, Npgsql (raw SQL in Identity module), EF Core + Npgsql (migrations in Messaging module), Zustand v5, Tailwind CSS v4, shadcn/ui, `react-easy-crop` (already installed from 014) (015-onboarding-wizard)
- PostgreSQL — one new column on `users` table; no new tables (015-onboarding-wizard)
- C# (.NET 8) — backend; TypeScript 5.9 / React 19 — frontend + ASP.NET Core SignalR, MediatR, EF Core (Npgsql), Zustand v5, Tailwind CSS v4, `@tanstack/react-virtual` (new frontend dep), StackExchange.Redis (016-performance-optimizations)
- PostgreSQL (EF Core + raw Npgsql); Redis (StackExchange.Redis) for presence (016-performance-optimizations)
- C# (.NET 8) — backend; TypeScript 5.9 / React 19 — frontend + ASP.NET Core SignalR, MediatR, EF Core (Npgsql), Zustand v5, Tailwind CSS v4, shadcn/ui (Sheet component already installed), lucide-react (`Bell` icon already available), react-markdown + remark-gfm (already in use) (017-mentions-notifs-mobile)
- PostgreSQL — one new table (`user_notifications`) added to the Notifications module via EF Core migration (017-mentions-notifs-mobile)
- C# (.NET 8) backend; TypeScript 5.9 / React 19 frontend + ASP.NET Core SignalR, MediatR, EF Core (Npgsql), Zustand v5, Tailwind CSS v4, lucide-reac (018-reaction-notifications)
- PostgreSQL — no new tables; existing `user_notifications` table via `NotificationsDbContext` (018-reaction-notifications)
- C# / .NET 8 (backend); TypeScript 5.9 / React 19 (frontend) + ASP.NET Core minimal API, MediatR, StackExchange.Redis (already present), EF Core + Npgsql, Zustand v5, Tailwind CSS v4, shadcn/ui, lucide-reac (019-admin-panel)
- PostgreSQL (EF Core for new `admin_audit_log` table via `AdminDbContext`); Redis/Valkey for token blocklist (existing instance) (019-admin-panel)

- C# (.NET 8 or 9) — backend; TypeScript — frontend + ASP.NET Core SignalR + `Microsoft.AspNetCore.SignalR.StackExchangeRedis`; Entity Framework Core; MediatR; `Microsoft.AspNetCore.Authentication.JwtBearer`; React; `@microsoft/signalr`; `idb`; Zustand; Tailwind CSS; shadcn/ui (001-chat-mvp)

## Project Structure

```text
src/
tests/
```

## Commands

npm test && npm run lint

## Code Style

C# (.NET 8 or 9) — backend; TypeScript — frontend: Follow standard conventions

## Recent Changes
- 019-admin-panel: Added C# / .NET 8 (backend); TypeScript 5.9 / React 19 (frontend) + ASP.NET Core minimal API, MediatR, StackExchange.Redis (already present), EF Core + Npgsql, Zustand v5, Tailwind CSS v4, shadcn/ui, lucide-reac
- 018-reaction-notifications: Added C# (.NET 8) backend; TypeScript 5.9 / React 19 frontend + ASP.NET Core SignalR, MediatR, EF Core (Npgsql), Zustand v5, Tailwind CSS v4, lucide-reac
- 017-mentions-notifs-mobile: Added C# (.NET 8) — backend; TypeScript 5.9 / React 19 — frontend + ASP.NET Core SignalR, MediatR, EF Core (Npgsql), Zustand v5, Tailwind CSS v4, shadcn/ui (Sheet component already installed), lucide-react (`Bell` icon already available), react-markdown + remark-gfm (already in use)


<!-- MANUAL ADDITIONS START -->
## Dependency Version Lock

**MediatR is locked at ≤ 12.5.0.** Do NOT upgrade to v13 or later. MediatR went commercial
(paid license) starting with v13.0.0 (July 2025). Version 12.5.0 is the last Apache 2.0
release. Any PR or task that bumps MediatR beyond 12.5.0 MUST be rejected.
<!-- MANUAL ADDITIONS END -->
