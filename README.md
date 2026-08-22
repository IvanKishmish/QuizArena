<div align="center">

# 🎮 QuizArena

### Real-time multiplayer quiz platform — a Kahoot of your own, built from scratch

*A pet project built to actually get hands-on with Clean Architecture, CQRS, and real-time gameplay — not just read about them in another tutorial.*

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-13-239120?logo=csharp&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-Web_API-512BD4?logo=dotnet&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-4169E1?logo=postgresql&logoColor=white)
![MongoDB](https://img.shields.io/badge/MongoDB-8-47A248?logo=mongodb&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-7-DC382D?logo=redis&logoColor=white)
![SignalR](https://img.shields.io/badge/SignalR-realtime-0078D4)
![Docker](https://img.shields.io/badge/Docker-compose-2496ED?logo=docker&logoColor=white)
![CI](https://img.shields.io/badge/CI-GitHub_Actions-2088FF?logo=githubactions&logoColor=white)
![React](https://img.shields.io/badge/React-19-149ECA?logo=react&logoColor=white)
![Status](https://img.shields.io/badge/backend-done-success)
![Status](https://img.shields.io/badge/frontend-in_progress-yellow)

</div>

---

> **This is a learning pet project, and that's not an excuse — it's a deliberate choice.**
> The goal was never "ship an MVP over a weekend," but to genuinely build up architectural muscle: a clean, layered architecture, CQRS, a real real-time game loop over SignalR, and a conscious choice of database for each kind of data instead of shoving everything into one table. Some decisions here are intentionally "over-engineered" for a project of this size — that's a deliberate trade-off made for the sake of practice, not an oversight.

<br>

## 📑 Table of contents

- [💡 About the project](#-about-the-project)
- [🏗️ High-level architecture](#️-high-level-architecture)
- [⚙️ Part 1 — Backend](#️-part-1--backend)
  - [Tech stack](#-tech-stack)
  - [Clean Architecture under the hood](#-clean-architecture-under-the-hood)
  - [Why three different data stores](#-why-three-different-data-stores-instead-of-one)
  - [Features](#-features)
  - [How a live game actually works](#-how-a-live-game-actually-works-step-by-step)
  - [REST API](#-rest-api)
  - [SignalR Hub — the real-time layer](#-signalr-hub--the-real-time-layer)
  - [Security & resilience](#-security--resilience)
  - [Testing & performance research](#-testing--performance-research)
  - [Infrastructure & deployment](#-infrastructure--deployment)
  - [Backend repository layout](#-backend-repository-layout)
  - [Quick start](#-quick-start)
- [🎨 Part 2 — Frontend](#-part-2--frontend)
- [🗺️ Project status](#️-project-status)
- [👥 Authors](#-authors)

<br>

## 💡 About the project

**QuizArena** is a backend (and, eventually, frontend) for hosting live, Kahoot-style quiz games. A host builds a set of questions, spins up a game room from it, players join by room code with no account required — and the game runs live: question by question, each with its own timer, with a leaderboard that updates after every single answer.

The point of the project isn't "yet another CRUD app" — it's a sandbox for practicing things that are genuinely hard to learn from tutorials alone:

- structuring a Clean Architecture solution across several independent projects with the dependency rule pointing the right way;
- CQRS through a lightweight mediator, rather than one bloated "service layer" class;
- an actual real-time game loop over SignalR — with room groups, reconnection handling, and state synchronization;
- a deliberate choice of data store for each kind of data (relational / document / in-memory), instead of "everything in Postgres because that's the default";
- and, as a bonus, a few small performance research experiments — comparing concurrency-control strategies and system behavior under load (more on that below, in the testing section).

<br>

## 🏗️ High-level architecture

```
                         ┌─────────────────────────┐
                         │         Clients           │
                         │  REST (HTTP) + SignalR   │
                         └────────────┬─────────────┘
                                      │
                         ┌────────────▼─────────────┐
                         │      QuizArena.WebApi      │
                         │  Controllers · GameHub      │
                         │  JWT Auth · Rate Limiting   │
                         └────────────┬─────────────┘
                                      │  CQRS (Mediator)
                         ┌────────────▼─────────────┐
                         │   QuizArena.Application     │
                         │  Commands/Queries · FluentValidation │
                         │  Domain events · Outbox     │
                         └────────────┬─────────────┘
                                      │  interfaces implemented externally
                         ┌────────────▼─────────────┐
                         │      QuizArena.Domain       │
                         │   entities & business rules  │
                         │       zero external dependencies │
                         └────────────▲─────────────┘
                                      │  implementations
                         ┌────────────┴─────────────┐
                         │    QuizArena.Persistence    │
                         ├───────────┬───────┬───────┤
                         │ PostgreSQL│ MongoDB│ Redis │
                         │ (EF Core) │(questions)│(live game)│
                         └───────────┴───────┴───────┘
```

The dependency rule always points inward: `Domain` knows nothing about Postgres, Mongo, or Redis, `Application` only knows abstractions, and concrete technologies are plugged in from the outside, in `Persistence` and `WebApi`. That's what makes it possible to test the business logic without ever touching a real database.

<br>

## ⚙️ Part 1 — Backend

<div align="center">

### 🧑‍💻 Backend author — **[IvanKishmish](https://github.com/IvanKishmish)**

The entire backend — architecture, domain model, CQRS layer, three data-store integrations, real-time gameplay over SignalR, authentication, admin panel, testing at every level (unit / integration / benchmark / load-test) and Docker/CI infrastructure — was built end to end, solo.

</div>

### 📊 Tech stack

| Category | Technology | Why it's here |
|---|---|---|
| Language / runtime | **C# 13 / .NET 10** | latest platform, modern language features |
| Web API | **ASP.NET Core Web API** | battle-tested HTTP layer |
| Architecture | **Clean Architecture** (4 projects) | dependencies always point inward, the domain stays framework-agnostic |
| CQRS / mediator | **Mediator** | a lighter-weight alternative to MediatR for command/query separation |
| Validation | **FluentValidation** | declarative validation of commands/queries |
| Error handling | **ErrorOr** | result-pattern error handling instead of exceptions for control flow |
| Relational data | **PostgreSQL 17 + EF Core** | users, quiz sets, game history — needs integrity and relations |
| Document data | **MongoDB 8** | questions — flexible, nested structure that varies by question type |
| In-memory data | **Redis 7** | active rooms, live leaderboard, WebSocket connection tracking |
| Real-time | **SignalR** (+ Redis backplane) | rooms, questions, answers and the leaderboard are pushed live; the backplane lets the hub scale across multiple instances |
| Auth | **ASP.NET Core Identity + JWT** | access + refresh tokens, refresh-token rotation by family id |
| Logging | **Serilog** | structured logging to console and file |
| Email | **MailKit / MimeKit + SMTP**, via the **Outbox** pattern | sending mail never blocks the main command flow |
| API docs | **Scalar** | interactive API documentation in Development, a modern alternative to Swagger UI |
| Rate limiting | **ASP.NET Core RateLimiter** | dedicated limits for auth, join-room, and a global per-IP limit |
| Tests | **xUnit + FluentAssertions + Testcontainers** | unit tests on Domain/Application, integration tests against real Postgres/Mongo/Redis containers |
| Performance | **BenchmarkDotNet** + custom load/consistency research tools | micro-benchmarks and measurements under real load |
| Infrastructure | **Docker + docker-compose** (+ Nginx for the cluster profile) | one command brings every service up together |
| CI | **GitHub Actions** | restore → check for vulnerable packages → build → unit tests → integration tests |

### 🧩 Clean Architecture under the hood

The backend is deliberately split into four separate projects — not for show, but because each layer has its own responsibility and its own set of dependencies:

- **`QuizArena.Domain`** — entities (`QuizSet`, `Question`, `GameRoom`, `Participant`, `Player`, `GameHistoryEntry`), enums (`QuestionType`, `PowerUpType`, `GameRoomStatus`, `Visibility`), and business rules. No dependency on any external library — this is the "heart" of the domain.
- **`QuizArena.Application`** — CQRS commands/queries grouped by feature (`Auth`, `QuizSets`, `Questions`, `GameRooms`, `GameHistory`, `Admin`), FluentValidation validators, domain events, and interfaces (`IGameRoomStore`, `IQuestionStore`, `ILeaderboardStore`, `IGameNotifier`, `IIdentityService`, etc.) implemented by the outer layers.
- **`QuizArena.Persistence`** — the actual implementations: EF Core over PostgreSQL (two `DbContext`s — the application one and the Identity one), a MongoDB driver for questions, StackExchange.Redis for live game state, and the Outbox mechanism for reliable event delivery.
- **`QuizArena.WebApi`** — controllers, the SignalR hub, DI wiring, middleware, rate limiting, global exception handling.

### 🗃 Why three different data stores, instead of one

This is arguably the most interesting architectural decision in the project — and it's deliberate, not incidental:

| Store | What it holds | Why it's the right fit |
|---|---|---|
| **PostgreSQL** | users, quiz sets, game history, refresh tokens | durable relational data that needs integrity and relations between tables |
| **MongoDB** | questions and answer options | different question types (`SingleChoice`, `MultipleChoice`, `TrueFalse`, `Ordering`) have different nested shapes — a document model fits far more naturally than a rigid relational schema |
| **Redis** | active game rooms, participant snapshots, the live leaderboard, WebSocket connection tracking, the SignalR backplane | ephemeral data that needs fast reads/writes, not durability — it has no business sitting in a relational store at all |

Honestly, part of the motivation was also getting real, hands-on experience running all three side by side in one system. For a learning project, that's a perfectly fair reason on its own.

### ✨ Features

**🔐 Accounts & access**
- Registration, login, logout — built on ASP.NET Core Identity
- JWT access token + refresh token in an httpOnly cookie, rotated by family id (a compromised refresh token invalidates the entire family)
- Background cleanup of expired refresh tokens (`RefreshTokenCleanupService`)

**📝 Quiz authoring**
- Create, edit, and delete quiz sets (`QuizSet`)
- Public / private visibility — authors see all of their own quizzes, everyone else only sees published ones
- Question types: **Single Choice**, **Multiple Choice**, **True/False**, **Ordering**
- A per-question time limit that directly affects how points are scored

**🕹️ Live gameplay**
- Room lifecycle: `Waiting → InProgress → Finished`
- The host creates a room from a quiz set and gets a short room code
- Players join anonymously by room code and nickname — no account required
- Questions are pushed one by one over SignalR, each with its own timer
- Points reward not just correctness but also answer speed
- Power-ups: **Freeze** (lock out an opponent for a short time), **Fifty-Fifty** (remove two wrong answers), **Double or Nothing** (double your points or lose them all)
- The leaderboard updates live after every single answer
- Reconnection: if a player reconnects, `GameHub` restores their current game state (current question index, elapsed time)
- The game result is saved at the end and available later from the player's account

**🛡️ Admin (API-level)**
- Browse all users with pagination, ban / unban
- Moderate and force-delete any quiz set
- A dashboard with aggregated stats

**📬 Notifications**
- A welcome email on registration, a results email after a game ends
- Neither is sent directly inside a command handler — both go through domain events → **Outbox** → a background dispatcher that sends mail over SMTP (MailKit). That means an SMTP outage never blocks or fails the main operation.

### 🎯 How a live game actually works, step by step

1. The host creates a quiz set and publishes it (or keeps it private).
2. The host creates a game room from that quiz set and gets a room code.
3. Players join the room by code, picking a nickname — no account needed.
4. The host starts the game; the room moves to `InProgress`.
5. Questions are pushed one by one over SignalR, each with its own timer.
6. Players submit answers; points are calculated from correctness and speed.
7. A power-up can be triggered mid-round to swing the game.
8. After every answer, the updated leaderboard is broadcast to the whole room.
9. After the last question, the room moves to `Finished`, final standings are shown, and the game is saved to history.
10. Players receive a results email and can look the game up later from their account.

### 🔌 REST API

Each controller's base path is `api/[controller]`, except `Questions`, which is nested under `QuizSets`.

#### `api/Auth` — rate-limited with the `auth` policy (10 requests/min)

| Method | Route | Access | Description |
|---|---|---|---|
| `POST` | `/register` | public | registration; returns an access token, refresh token in an httpOnly cookie |
| `POST` | `/login` | public | login |
| `POST` | `/refresh` | public (via cookie) | refresh the token pair |
| `POST` | `/logout` | JWT | logout, invalidates the refresh token |

#### `api/QuizSets`

| Method | Route | Access | Description |
|---|---|---|---|
| `POST` | `/` | JWT | create a quiz set |
| `GET` | `/{id}` | public | get a quiz set by id |
| `GET` | `/my` | JWT | my quiz sets (including private ones) |
| `GET` | `/public?pageNumber&pageSize` | public | published quiz sets, paginated |
| `PUT` | `/{id}` | JWT | update title/description |
| `POST` | `/{id}/publish` | JWT | publish |
| `POST` | `/{id}/unpublish` | JWT | unpublish |
| `DELETE` | `/{id}` | JWT | delete |

#### `api/quizsets/{quizSetId}/questions`

| Method | Route | Access | Description |
|---|---|---|---|
| `POST` | `/` | JWT | add a question with answer options |
| `GET` | `/` | JWT | list the quiz set's questions |
| `DELETE` | `/{questionId}` | JWT | delete a question |

#### `api/GameRooms`

| Method | Route | Access | Description |
|---|---|---|---|
| `POST` | `/` | JWT | create a room from a quiz set |
| `POST` | `/{roomCode}/join` | public, rate-limited with `join-room` (20/min) | join with a nickname |
| `POST` | `/{roomCode}/start` | JWT (host) | start the game |

#### `api/GameHistory`

| Method | Route | Access | Description |
|---|---|---|---|
| `GET` | `/my?pageNumber&pageSize` | JWT | my game history |

#### `api/Admin` — `Admin` role only

| Method | Route | Description |
|---|---|---|
| `GET` | `/users?pageNumber&pageSize` | list users |
| `POST` | `/users/{userId}/ban` | ban |
| `POST` | `/users/{userId}/unban` | unban |
| `GET` | `/quizsets?pageNumber&pageSize` | all quiz sets, for moderation |
| `DELETE` | `/quizsets/{quizSetId}` | force-delete any quiz set |
| `GET` | `/dashboard` | aggregated stats |

In Development mode, full interactive API docs are available via **Scalar** right after startup.

### 📡 SignalR Hub — the real-time layer

The hub lives at **`/hubs/game`**. For the WebSocket connection, the JWT is passed via the `access_token` query parameter (the usual cookie-based flow doesn't apply here).

**Client → server**

| Method | Description |
|---|---|
| `RegisterParticipant(roomCode, participantId, participantToken)` | registers the player's connection, validates the participant token, restores game state on reconnect |
| `JoinRoomGroup(roomCode)` / `LeaveRoomGroup(roomCode)` | join/leave the room's SignalR group |
| `NextQuestion(roomCode)` | advance to the next question — host only |
| `SubmitAnswer(roomCode, participantId, questionId, selectedOptionIndices)` | submit an answer |
| `UsePowerUp(roomCode, participantId, powerUpType, targetParticipantId)` | activate a power-up |
| `EndGame(roomCode)` | end the game — host only |

**Server → client**

| Event | Recipient | Description |
|---|---|---|
| `QuestionStarted` | whole room | a new question has started |
| `ParticipantJoined` | whole room | a new player joined |
| `LeaderboardUpdated` | whole room | leaderboard updated after an answer |
| `PowerUpUsed` | whole room | someone activated a power-up |
| `GameFinished` | whole room | the game has ended, final standings |
| `AnswerResult` | caller only | the result of that caller's own answer |
| `GameStateRestored` | caller only | restored game state after reconnecting |
| `Error` | caller only | an error while executing a hub command |

Every game-changing hub call verifies that the caller really is the participant they claim to be (`IConnectionTracker`), and host-only commands (`NextQuestion`, `EndGame`) verify that the caller is the actual room owner (`HostId`), checked against the JWT claim.

### 🔒 Security & resilience

- **JWT + refresh rotation**: the refresh token lives only in an httpOnly/secure cookie, a new one is issued on every refresh, and a token family id lets the whole chain be invalidated if compromise is suspected.
- **Rate limiting**: dedicated fixed-window limits on `auth` (10/min) and `join-room` (20/min), plus a global 300 requests/min per-IP limit across the whole API.
- **CORS** — strictly allow-listed origins from configuration; it's required, the app won't start without it.
- **Input validation** — every command and query goes through its own FluentValidation validator before it ever reaches business logic.
- **ErrorOr instead of exceptions** for expected business failures — exceptions are reserved for truly exceptional situations and caught by the global `GlobalExceptionHandler`.
- **Outbox pattern** for email — guarantees delivery even if SMTP is temporarily unavailable, without blocking the main request.

### 🧪 Testing & performance research

Testing here isn't a single "just to check the box" layer — it's several layers, each catching a different class of bug.

- **`QuizArena.Domain.UnitTests`** — pure tests of domain entities and business rules, with no infrastructure mocks at all.
- **`QuizArena.Application.UnitTests`** — tests of CQRS handlers, validators, and domain events against fake store implementations.
- **`QuizArena.IntegrationTests`** — an end-to-end game scenario, from registration to the final leaderboard, spun up against **real** PostgreSQL, MongoDB, and Redis via **Testcontainers** — not mocks. This is exactly where the bugs unit tests can never see get caught: real Mongo serialization, real Redis snapshots, a real JWT round trip.
- **CI on GitHub Actions** — on every push/PR to `master`: restore dependencies → check for vulnerable NuGet packages → build in Release → unit tests → integration tests → publish trx reports.

And a research layer that goes beyond "just write tests":

- **`benchmarks/QuizArena.Benchmarks`** (BenchmarkDotNet) — a micro-benchmark of the `SubmitAnswer` hot path: running the core logic directly versus dispatching it through the mediator, to honestly measure the real overhead of the CQRS abstraction on the hottest path in the game.
- **`research/consistency`** — a dedicated study of concurrency-control strategies for simultaneous room joins: a naive read-check-then-write, PostgreSQL optimistic locking, and an atomic Redis strategy — all measured against each other on the same infrastructure, so the comparison is fair.
- **`research/loadtest`** — a custom load-testing client that drives a full game scenario against a multi-instance WebApi cluster behind Nginx, with the Redis SignalR backplane both on and off, recording latency percentiles (P50/P95/P99) to CSV for further analysis.

### 🐳 Infrastructure & deployment

`compose.yaml` brings up the full environment through two independent profiles:

- **`single`** — one `webapi1` instance, exposed directly on port `5000`. The simplest way to just run it and play.
- **`cluster`** — three instances (`webapi1`–`webapi3`) behind **Nginx** (`5001`), with the Redis SignalR backplane enabled — this is the profile used for the load-testing research above.

**PostgreSQL 17**, **MongoDB 8**, and **Redis 7-alpine** always come up alongside the app, each with its own healthcheck, so the `webapi` containers only start once their dependencies are actually ready. Database migrations and the admin account seed run automatically at startup in Development. The `Dockerfile` is multi-stage (`sdk` → `build` → `publish` → `aspnet` runtime image), so the final image doesn't carry the whole SDK with it.

### 📁 Backend repository layout

```
src/backend/
  QuizArena.Domain/          — entities, enums, business rules, no dependencies
    Entities/                — QuizSet, Question, GameRoom, Participant, Player, GameHistoryEntry
    Enums/                   — QuestionType, PowerUpType, GameRoomStatus, Visibility
  QuizArena.Application/     — CQRS commands/queries, validators, interfaces
    Features/                — Auth, QuizSets, Questions, GameRooms, GameHistory, Admin
    Common/                  — mediator behaviours, options, shared contracts
    Benchmarking/H1/         — isolated "core" used for the SubmitAnswer micro-benchmark
  QuizArena.Persistence/     — EF Core (Postgres), Mongo, Redis, Outbox, Identity
  QuizArena.WebApi/          — controllers, GameHub, DI, rate limiting, Dockerfile

tests/backend/
  QuizArena.Domain.UnitTests/
  QuizArena.Application.UnitTests/
  QuizArena.IntegrationTests/   — Testcontainers: Postgres + Mongo + Redis

benchmarks/QuizArena.Benchmarks/   — BenchmarkDotNet micro-benchmarks
research/
  consistency/   — comparison of concurrency-control strategies
  loadtest/      — load-testing client + Nginx config for the cluster
  results/       — CSV files with measurement results

compose.yaml       — orchestrates all services (single / cluster profiles)
```

### 🚀 Quick start

```bash
git clone https://github.com/IvanKishmish/QuizArena
cd QuizArena
cp .env.docker.example .env.docker   # fill in JWT secret, DB passwords, SMTP, etc.
docker compose --profile single up --build
```

This brings up PostgreSQL (`5432`), MongoDB (`27017`), Redis (`6379`), and the WebApi (`5000` → `8080` inside the container). Database migrations and the admin account seed run automatically. For the clustered scenario with Nginx and three instances:

```bash
docker compose --profile cluster up --build
```

In Development mode, interactive API docs are available via Scalar right after startup.

<br>

---

<br>

## 🎨 Part 2 — Frontend

<div align="center">

### 🧑‍🎨 Frontend author — **[nico3331](https://github.com/nico3331)**

</div>

<div align="center">

🚧 **The frontend is currently in progress** 🚧

The client is still being actively built, so there's no detailed write-up yet — this section will be updated once there's a stable, working feature set to show.

</div>

<br>

---

<br>

## 🗺️ Project status

| Part | Status |
|---|---|
| Backend: auth, quiz sets, full game logic, real-time over SignalR, admin panel | ✅ Done |
| Backend: unit & integration tests, benchmarks, load-testing & consistency research | ✅ Done |
| Backend: Docker infrastructure (single / cluster) and CI on GitHub Actions | ✅ Done |
| Frontend | 🚧 In progress |
| Telegram bot | 📋 Planned |

<br>

## 👥 Authors

| Role | Author |
|---|---|
| Backend | **[IvanKishmish](https://github.com/IvanKishmish)** |
| Frontend | **[nico3331](https://github.com/nico3331)** |

<div align="center">
<br>

⭐️ If the project looks interesting, check back later — the frontend is slowly coming to life.

</div>
