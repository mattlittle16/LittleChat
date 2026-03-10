# LittleChat Development Guidelines

##MOST IMPORTANT 
Don't ever assume, guess, or make up stats or theories without investigating the code base first. If you do need to propose a theory, you MUST tell me it's a theory rather than propose it as fact.

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
- 008-multi-file-attachments: Added C# (.NET 8) — backend; TypeScript 5.9 / React 19 — frontend + ASP.NET Core minimal API, EF Core (Npgsql), MediatR, SignalR, Zustand v5, Tailwind CSS v4, shadcn/ui, SixLabors.ImageSharp 3.x + SixLabors.ImageSharp.Formats.Heic (new)
- 007-chat-ux-fixes: Added TypeScript 5.9 / React 19 + Tiptap v2 (`@tiptap/react`, `@tiptap/starter-kit`), `react-markdown` + `remark-gfm`, Zustand v5, Tailwind CSS v4, shadcn/ui (slate/cssVariables), lucide-reac
- 006-inline-markdown-editor: Added TypeScript 5.9 / React 19 (frontend only) + Tiptap v2 (`@tiptap/react`, `@tiptap/starter-kit`), existing `react-markdown` + `remark-gfm` unchanged (still used in chat history display)


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
