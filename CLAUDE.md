# LittleChat Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-03-01

## Active Technologies
- C# (.NET 8) — backend; TypeScript/React — frontend + ASP.NET Core SignalR (`@microsoft/signalr` v10), Zustand, `emoji-picker-react` v4.18, Npgsql (PostgreSQL), MediatR, Entity Framework Core (002-fix-dm-emoji-bugs)
- PostgreSQL (Npgsql raw SQL in repositories; EF Core for schema/migrations in Messaging module) (002-fix-dm-emoji-bugs)
- TypeScript 5.9 / React 19 + Tailwind CSS v4 (`@tailwindcss/vite`), shadcn/ui (cssVariables mode, slate base), Zustand v5, Vite 7, lucide-react (icon library already installed) (003-visual-redesign)
- `localStorage` — key `littlechat_theme` (`'light'` | `'dark'`) (003-visual-redesign)
- C# (.NET 8) — backend; TypeScript 5.9 / React 19 — frontend + ASP.NET Core SignalR, MediatR, Npgsql, Zustand v5, Tailwind CSS v4, shadcn/ui (slate/cssVariables), lucide-reac (004-user-content-controls)
- PostgreSQL via Npgsql raw SQL in repositories (004-user-content-controls)

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
- 004-user-content-controls: Added C# (.NET 8) — backend; TypeScript 5.9 / React 19 — frontend + ASP.NET Core SignalR, MediatR, Npgsql, Zustand v5, Tailwind CSS v4, shadcn/ui (slate/cssVariables), lucide-reac
- 003-visual-redesign: Added TypeScript 5.9 / React 19 + Tailwind CSS v4 (`@tailwindcss/vite`), shadcn/ui (cssVariables mode, slate base), Zustand v5, Vite 7, lucide-react (icon library already installed)
- 002-fix-dm-emoji-bugs: Added C# (.NET 8) — backend; TypeScript/React — frontend + ASP.NET Core SignalR (`@microsoft/signalr` v10), Zustand, `emoji-picker-react` v4.18, Npgsql (PostgreSQL), MediatR, Entity Framework Core


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
