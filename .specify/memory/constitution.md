<!--
SYNC IMPACT REPORT
==================
Version change:       1.0.0 → 1.1.1
Bump type:            MINOR (1.1.0) — module list updated (Admin added); Principle IV exclusion
                      list corrected; Last Amended date updated.
                      PATCH (1.1.1) — MediatR version lock (≤12.5.0) added to Additional
                      Constraints. Apache 2.0 ceiling enforced due to v13+ commercial licensing.

Modified principles:
  II. Module Isolation — "Admin" module added to the canonical module list
  IV. Simplicity (YAGNI) — Removed "admin panel" and "multi-file uploads" from the
      hard-exclusion list because both have been deliberately implemented.
      Updated exclusion list to features genuinely out of scope at this time.

Added sections:       None
Removed sections:     None

Templates checked:
  ✅ .specify/templates/plan-template.md
       Constitution Check section is generic — no change needed. Plans evaluate
       Principles I–V at authoring time; the template wording is still correct.
  ✅ .specify/templates/spec-template.md
       No mandatory section additions required. Existing structure remains compatible.
  ✅ .specify/templates/tasks-template.md
       "Security hardening" in Polish phase still present. Module list in Principle II
       is checked at plan-time, not in the task template. No structural change needed.
  ✅ No .specify/templates/commands/ directory — no command files to update.
  ✅ No docs/ directory — no runtime guidance docs to update.
  ✅ CLAUDE.md — Constitution version reference is indirect; no inline version citation
       in CLAUDE.md to update. Covered by MEMORY.md pointer.

Deferred TODOs:       None — all sections fully resolved.
Follow-up:            Re-evaluate Principle V (outbox resilience) when full outbox
                      implementation is audited; the principle may need an amendment
                      if IndexedDB outbox was never completed for the MVP.
-->

# LittleChat Constitution

## Core Principles

### I. Data Integrity — Persist Before Broadcast

Every message, reaction, edit, or delete MUST be durably persisted to the database before
being broadcast to any connected client. A broadcast MAY only occur after a successful
database commit. Operations that fail to persist MUST NOT be broadcast and MUST be reported
as failures to the originating client.

**Rationale**: Phantom messages — messages seen by recipients but never stored — are a
correctness violation that cannot be undone after the fact. Storage-first guarantees that
every visible message is recoverable, auditable, and consistent across all clients.

**Gates this principle imposes on plans**:
- Any feature involving write + broadcast MUST document the persist-first sequence.
- REST and SignalR paths MUST share the same business logic (no shortcut broadcast path).

---

### II. Module Isolation — Compile-Time Boundaries

Each module (Identity, Messaging, Presence, Reactions, Search, Files, Notifications,
RealTime, Admin) MUST NOT directly reference classes, types, or namespaces from another
module's projects. Cross-module communication MUST occur exclusively through:

- `LittleChat.Shared.Contracts` (integration events, shared interfaces, shared DTOs)
- `IEventBus` (integration events dispatched asynchronously between modules)
- `IRealtimeNotifier` (real-time push from any module to connected clients)

Boundary compliance MUST be enforced at compile time via `.csproj` project references and
validated in CI via `NetArchTest.Rules` architecture tests.

**Rationale**: Mechanical boundary enforcement prevents the coupling that makes monoliths
hard to maintain. Modules that cannot import each other's internals can be reasoned about,
tested, and eventually extracted without untangling hidden dependencies.

**Gates this principle imposes on plans**:
- Every new feature MUST be assigned to exactly one module or placed in `Shared.Contracts`
  if consumed by more than one module.
- Any cross-module data flow MUST be modelled as an integration event, not a direct call.
- Architecture tests (NetArchTest) MUST run in CI from the first commit that introduces
  new module structure.

---

### III. Security by Default — Auth-First Design

All API endpoints and SignalR hub methods MUST require a valid authenticated session.
No endpoint MAY be publicly accessible unless it is exclusively part of the OIDC
authentication flow (`GET /auth/login`, `GET /auth/callback`).

Additionally:
- File access (`GET /api/files/{messageId}`) MUST validate that the requesting user is a
  member of the room the file belongs to.
- Direct Message content MUST NEVER appear in global search results.
- JWT tokens are validated via the Authentik OIDC authority on every request; the `sub`
  claim is the sole stable user identifier — display names MUST NOT be used as identity keys.
- Access tokens MUST be stored in `localStorage` to support session persistence across
  browser close, accepting the XSS tradeoff as appropriate for this small trusted group.
- Admin-only endpoints MUST use a dedicated authorization policy that verifies the
  `admin` role claim; they MUST NOT be accessible to non-admin authenticated users.

**Rationale**: This application stores private communications. Unauthenticated access
to any data endpoint is a confidentiality failure. The user population is small and trusted,
making the localStorage tradeoff acceptable in lieu of a full refresh-token rotation flow.

**Gates this principle imposes on plans**:
- Every new endpoint MUST be listed with its auth requirement in the REST or SignalR contract.
- Any endpoint serving user-generated content (files, messages) MUST include a membership
  or ownership validation step in its implementation design.
- Admin endpoints MUST explicitly name the authorization policy applied.

---

### IV. Simplicity — YAGNI for a Trusted Small Group

Features, abstractions, and infrastructure MUST be sized to the actual needs of the current
deployment (≤50 concurrent users, single server). Solutions MUST be the simplest design
that satisfies the stated requirement.

- The `InMemoryEventBus` is the correct MVP choice; replace with a durable broker only when
  the deployment genuinely requires it.
- List virtualisation, caching layers, and CDN offloading MUST NOT be introduced until
  profiling confirms they are needed.
- Features explicitly out of scope at this time (threaded replies, push/email notifications,
  per-user private rooms, mobile apps) MUST NOT be partially implemented as "hooks" or
  "future-proofing" without explicit approval.
- Hard deletes are used everywhere; soft deletes MUST NOT be introduced.

**Rationale**: Premature complexity increases maintenance cost, introduces bugs, and
makes the codebase harder to reason about — with no benefit to the current user group.
The 30-day TTL and `expires_at` column are the system's sole retention/cleanup mechanism.

**Gates this principle imposes on plans**:
- The Complexity Tracking table in plan.md MUST be filled whenever a design choice
  adds more than 3 projects, introduces a new infrastructure dependency, or adds a
  pattern (e.g., repository, CQRS) not already established in the codebase.
- Any scope creep beyond the active spec MUST be rejected at plan or task review time.

---

### V. Resilience — No Message Left Behind

The client MUST maintain a durable outbox (IndexedDB) for all unconfirmed messages.
An outbox message MUST NOT be deleted until the server has acknowledged successful
persistence. The outbox MUST survive page refreshes and full browser close/reopen.

On reconnection, the client MUST automatically drain the outbox in composition order
without user intervention. A manual "Retry all" action MUST also be available.

The SignalR backplane (Valkey) MUST be configured with `AbortOnConnectFail = false`
so that the application starts and remains available even if Valkey is temporarily
unreachable — degrading gracefully rather than failing hard.

**Rationale**: Temporary disconnections are routine. Users MUST be able to trust that
messages they composed are either delivered or clearly shown as pending — never silently
dropped. Resilience is a correctness property, not a nice-to-have.

**Gates this principle imposes on plans**:
- Any messaging feature MUST document its behaviour during disconnection (outbox entry
  lifecycle, retry trigger, failure state).
- Infrastructure that has `AbortOnConnectFail` semantics MUST explicitly set it to `false`.

---

## Additional Constraints

- **Deployment target**: Linux Docker containers, self-hosted on a single Ubuntu server.
  All configuration is injected via environment variables. Secrets MUST NEVER be committed
  to source control or stored in files on disk — use GitHub Secrets / environment injection.
- **Message TTL**: All messages expire after 30 days (`expires_at` column). The daily
  cleanup job processes file deletion before row deletion; a file MUST be deleted before
  its message row is removed. If file deletion fails, the row is preserved until the
  next run.
- **No soft deletes**: Hard deletes are used everywhere. `DELETE FROM messages WHERE id = $1
  AND user_id = $2`. Reactions cascade-delete when their parent message is deleted.
- **Operator-configurable batch size**: The initial message history load size is controlled
  by the `MESSAGE_PAGE_SIZE` environment variable (default: 100). This MUST remain
  configurable at deployment time without code changes.
- **Idempotency**: Message creation uses client-generated UUIDs as primary keys.
  Duplicate inserts with the same `clientId` MUST be silently ignored by the server.
- **MediatR version lock**: MediatR MUST NOT be upgraded beyond **12.5.0**. v13+ moved to
  a commercial license (RPL-1.5 / paid). 12.5.0 is the last Apache 2.0 release and is the
  permanent ceiling for this project. Any PR bumping MediatR above 12.5.0 MUST be rejected.
- **System messages**: Messages sent by the system (ban notices, join/leave events) MUST
  use `user_id = NULL` as the discriminator. The `author_display_name` column MUST be
  persisted for system messages so the sender name survives page reload. System messages
  MUST NOT increment unread counts or trigger mention notifications.

---

## Development Workflow

- **Speckit workflow**: All features follow `/speckit.specify` → `/speckit.clarify` →
  `/speckit.plan` → `/speckit.tasks` → `/speckit.implement`.
- **Constitution Check gate**: Every `plan.md` MUST include a Constitution Check section
  that evaluates compliance with Principles I–V before Phase 0 research begins, and
  re-evaluates after Phase 1 design. Non-compliant designs MUST be rejected or justified
  in the Complexity Tracking table.
- **Architecture tests**: `LittleChat.Tests.Architecture` (NetArchTest.Rules) MUST run on
  every CI build. A failing architecture test MUST block the build.
- **Module assignment**: Before implementing a new feature, its owning module MUST be
  identified. If no existing module fits, a new module MUST be proposed and reviewed
  before implementation begins.
- **No `--no-verify` bypasses**: Pre-commit hooks and CI gates MUST NOT be bypassed.
  If a hook fails, investigate and fix the root cause.

---

## Governance

This constitution supersedes all other implicit practices. Amendments require:

1. A documented rationale explaining why the principle needs to change.
2. An assessment of which existing features or tasks are affected by the change.
3. A migration plan for any in-progress work that would be invalidated.
4. A version bump according to semantic versioning:
   - **MAJOR**: Principle removed, redefined in a backward-incompatible way, or scope
     of the application fundamentally changed.
   - **MINOR**: New principle or section added, or material guidance expanded.
   - **PATCH**: Clarifications, wording improvements, typo fixes, non-semantic changes.

For any work that does database changes, you MUST use Entity Framework tools to generate
migrations. Do not attempt to create them manually.

All pull requests MUST verify compliance with each applicable principle before merge.
Complexity MUST be justified; default answer to "should we add this?" is no.

**Version**: 1.1.1 | **Ratified**: 2026-03-01 | **Last Amended**: 2026-03-21
