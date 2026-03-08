<div align="center">

# 🗨️ MattLab Chat

**Self-hosted real-time group chat. Own your conversations.**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-19-61DAFB?logo=react&logoColor=black)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.9-3178C6?logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white)](https://docs.docker.com/compose/)

*Slack-inspired, deliberately minimal, runs entirely on your own infrastructure.*

</div>

---

## ✨ Features

| | |
|---|---|
| 💬 **Real-time messaging** | WebSocket-powered via ASP.NET Core SignalR |
| 🏠 **Rooms & Direct Messages** | Same underlying primitive, unified experience |
| ✍️ **Inline markdown editor** | *italic*, **bold**, `code`, ~~strikethrough~~ as you type |
| 👍 **Reactions** | Emoji reactions on any message |
| 📎 **File attachments** | Up to 200 MB per message |
| @ **Mentions** | Autocomplete with browser and toast notifications |
| 🟢 **Live presence** | Real-time online/offline indicators |
| 🔍 **Full-text search** | Across all rooms |
| ✏️ **Edit & delete** | Your own messages, always |
| 🔔 **Notification settings** | Per-room: all messages, mentions only, or muted |
| 🔐 **OIDC authentication** | Any OIDC-compliant identity provider — no passwords, no registration |
| 📦 **Offline resilience** | IndexedDB outbox queues messages during connectivity gaps |

---

## 🏗️ Architecture

```mermaid
graph LR
    Browser -->|HTTPS / WSS| NPM[Nginx Proxy Manager]
    NPM -->|HTTP| nginx[nginx reverse proxy]
    nginx -->|REST + WebSocket| Backend[ASP.NET Core API]
    nginx -->|Static files| Frontend[React SPA]
    Backend -->|SQL| DB[(PostgreSQL)]
    Backend -->|Pub/Sub| Valkey[(Valkey)]
    Backend -->|OIDC| Authentik[Authentik IdP]
```

The backend is a **modular monolith** — eight vertical modules deployed as a single unit with compile-time isolation enforced by architecture tests. Cross-module communication uses two patterns:

- **Shared interfaces** (`IPresenceService`, `IRealtimeNotifier`, `IUserLookupService` …) — for direct cross-module calls via DI, without exposing internal implementations
- **Integration events** (`MessageSentIntegrationEvent`, `UserFirstLoginIntegrationEvent` …) — for decoupled notifications via an in-memory event bus

Neither pattern creates a hard dependency on another module's internals, keeping each module independently extractable if scaling ever demands it.

---

## 🛠️ Tech Stack

| Layer | Technology |
|---|---|
| **Backend** | C# / .NET 8, ASP.NET Core, MediatR |
| **Real-time** | ASP.NET Core SignalR + Valkey (Redis-compatible) backplane |
| **Database** | PostgreSQL — raw SQL via Npgsql, EF Core for migrations |
| **Frontend** | React 19, TypeScript 5.9, Vite 7 |
| **Styling** | Tailwind CSS v4, shadcn/ui (slate theme) |
| **State** | Zustand v5 |
| **Editor** | Tiptap v2 (inline markdown compose box) |
| **Auth** | OIDC → JWT Bearer tokens |
| **Infrastructure** | Docker Compose, nginx, Nginx Proxy Manager |
| **CI/CD** | GitHub Actions → self-hosted runner |

---

## 🚀 Getting Started

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [.NET SDK 8.x](https://dotnet.microsoft.com/download) *(backend development only)*
- [Node.js 20+](https://nodejs.org/) *(frontend development only)*
- An OIDC-compliant identity provider (e.g. Authentik, Keycloak, Auth0)

### 1. Configure your environment

```bash
git clone <repo-url> mattlab-chat
cd mattlab-chat
cp .env.example .env
```

Open `.env` and fill in your values — see `.env.example` for all available options with inline documentation.

### 2. Configure your identity provider

Create an **OAuth2/OpenID Connect** application in your identity provider:

| Setting | Value |
|---|---|
| Client type | Confidential |
| Redirect URI | `http://localhost:3000/auth/callback` |

Paste the generated Client ID, Client Secret, and Authority URL into your `.env`.

### 3. Run it

```bash
docker compose up
```

| Service | URL |
|---|---|
| App | http://localhost:3000 |
| Backend API | http://localhost:5000 |
| SignalR Hub | ws://localhost:5000/hubs/chat |

Both the backend (`dotnet watch`) and frontend (Vite) support hot reload — file changes reflect immediately.

### 4. Run the tests

```bash
cd src/backend
dotnet test tests/Unit/
dotnet test tests/Architecture/
```

---

## 📁 Project Structure

```
├── src/
│   ├── backend/
│   │   ├── src/
│   │   │   ├── API/                  # Composition root — Program.cs, middleware
│   │   │   ├── Shared/               # Contracts, interfaces, shared infrastructure
│   │   │   └── Modules/
│   │   │       ├── Identity/         # User sync, OIDC claims
│   │   │       ├── Messaging/        # Rooms, DMs, messages
│   │   │       ├── Presence/         # Online/offline tracking (Valkey TTL)
│   │   │       ├── Reactions/        # Emoji reactions
│   │   │       ├── Search/           # Full-text message search
│   │   │       ├── Files/            # File upload and serving
│   │   │       ├── Notifications/    # Per-room notification preferences
│   │   │       └── RealTime/         # SignalR hub, event handlers
│   │   └── tests/
│   │       ├── Unit/
│   │       └── Architecture/         # NetArchTest module boundary enforcement
│   └── frontend/
│       └── src/
│           ├── components/
│           ├── hooks/
│           ├── services/             # API client, SignalR client, auth
│           └── stores/               # Zustand state (messages, rooms, presence…)
├── docker/nginx/nginx.conf           # Reverse proxy config
├── docker-compose.yml                # Local dev
├── docker-compose.prod.yml           # Production
└── .env.example                      # All config options documented
```

---

## 🌐 Production Deployment

Deployment is fully automated via GitHub Actions on every push to `master`.

**Server prerequisites:**
- Docker + Docker Compose
- Self-hosted GitHub Actions runner
- Nginx Proxy Manager (or equivalent) for TLS termination
- A shared Docker network named `app-network`

```bash
# One-time server setup
docker network create app-network
```

Configure **GitHub Secrets** for every variable listed in `.env.example` — the CI workflow assembles the `.env` at deploy time. Secrets are never stored on disk between deployments.

> **Note:** Enable WebSocket proxying on your NPM proxy host for the backend — required for SignalR connections.

---

## 💡 Design Decisions

A few intentional choices worth understanding:

- **Persist-before-broadcast** — messages are written to the database before being broadcast. No phantom messages.
- **Modular monolith** — modules deploy together but are architecturally isolated; boundaries are enforced by automated architecture tests.
- **Hard deletes only** — no soft deletes anywhere in the codebase.
- **Auto-join** — all users are automatically added to all rooms. New rooms auto-add all existing users.
- **30-day message TTL** — messages are hard-deleted after 30 days by a background cleanup service.
- **Single file per message** — one attachment per message, 200 MB max.
- **JWT in localStorage** — an accepted tradeoff to support the offline-first IndexedDB outbox.
- **DMs excluded from search** — global search covers public rooms only.

---

## 📄 License

[MIT](./LICENSE) © 2026 Matt Little
