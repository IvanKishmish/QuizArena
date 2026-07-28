# 🎮 QuizArena

A real-time multiplayer quiz backend — think Kahoot, built from scratch to dig deep into Clean Architecture, CQRS, and polyglot persistence.

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?logo=postgresql&logoColor=white)
![MongoDB](https://img.shields.io/badge/MongoDB-47A248?logo=mongodb&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-DC382D?logo=redis&logoColor=white)
![SignalR](https://img.shields.io/badge/SignalR-realtime-0078D4)
![Docker](https://img.shields.io/badge/Docker-2496ED?logo=docker&logoColor=white)

> **This is a pet / learning project — and that's the point.** It's not a production SaaS, it's a sandbox for practicing serious backend architecture: Clean Architecture, CQRS, real-time systems with SignalR, and picking the right database for the right job instead of forcing everything into one. Some choices here are deliberately over-engineered for a project this size — that's intentional, not a mistake.

---

## About the project

QuizArena is a backend for hosting live, Kahoot-style quiz games. A host builds a quiz set, spins up a game room, players join by room code, and the game runs through questions in real time with a live, constantly-updating leaderboard.

I built it to get real, hands-on practice with a few things that are hard to learn from tutorials alone: structuring a Clean Architecture solution across multiple projects, applying CQRS with a lightweight mediator instead of reaching for the usual suspects, building an actual real-time game loop over SignalR, and making deliberate calls about where each piece of data should live — relational, document, or in-memory.

## ✨ Key features

**Accounts & access**
- Registration, login, JWT access + refresh tokens, logout — built on ASP.NET Core Identity

**Quiz authoring**
- Create, edit, and delete quiz sets
- Public/Private visibility — authors see all their own quizzes, everyone else only sees published ones
- Question types: Single Choice, Multiple Choice, True/False, Ordering
- Per-question time limits that directly affect scoring

**Live gameplay**
- Game rooms with `Waiting → InProgress → Finished` lifecycle
- Host creates a room from a quiz set; players join anonymously by nickname + room code
- Questions run in sequence with a timer, broadcast over SignalR
- Scoring rewards faster correct answers
- Power-ups: **Freeze** (lock out an opponent for a bit), **Fifty-Fifty** (remove two wrong answers), **Double or Nothing** (double your points or lose them all)
- Leaderboard updates live after every single answer
- Game history saved at the end and viewable later from the player's account

**Admin**
- API-level admin panel: view all users, ban/unban, moderate or remove any quiz set, stats dashboard

**Notifications**
- Welcome email on registration, results email after a game ends — both event-driven via domain notifications, not bolted onto the handlers

## 🏗️ Architecture

The backend follows Clean Architecture, split across four projects:

- **Domain** — entities, enums, and business rules. Zero dependencies on anything else.
- **Application** — CQRS commands/queries (via **Mediator**, a lighter alternative to MediatR), FluentValidation validators, and the interfaces the outer layers implement.
- **Persistence** — the actual implementations: EF Core over PostgreSQL, MongoDB driver, Redis.
- **WebApi** — controllers, the SignalR hub, DI wiring, and configuration.

The dependency rule only points inward, which is the whole reason this pays off: business logic in Domain/Application doesn't know or care that Postgres, Mongo, or Redis exist.

**Why three different stores, not one?** Because different parts of this domain genuinely have different shapes and lifetimes:
- **PostgreSQL** holds durable, relational data — users, quiz sets, game history. It needs integrity and it needs to last.
- **MongoDB** holds questions. Questions have a flexible, nested structure (varying answer options per type), which maps naturally onto documents instead of a rigid relational schema.
- **Redis** holds ephemeral game state — active rooms, connected players, the live leaderboard — plus WebSocket connection tracking. This data is short-lived and needs to be read and written fast; it has no business sitting in a durable relational store.

Honestly, part of the goal was also just to get real experience with all three in one system — that's a fair thing to admit for a learning project.

## 🧩 How a game actually works

1. Host creates a quiz set and publishes it (or keeps it private).
2. Host creates a game room from that quiz set — gets a room code.
3. Players join the room by code, picking a nickname, no account required.
4. Host starts the game; the room moves to `InProgress`.
5. Questions are pushed one by one over SignalR, each with its own timer.
6. Players submit answers; points are calculated based on correctness and answer speed.
7. Power-ups can be triggered mid-game to swing the round.
8. The leaderboard broadcasts an update after every answer, to everyone in the room.
9. After the last question, the room moves to `Finished`, final standings are shown, and the result is persisted as game history.
10. Players get an email with their results and can look the game up later from their account.

## 🛠️ Tech stack

| Category | Technology | Why it's here |
|---|---|---|
| Runtime / Language | .NET 10, C# | Latest LTS-track platform, modern language features |
| API | ASP.NET Core Web API | Standard, battle-tested HTTP layer |
| Architecture | Clean Architecture (4 projects) | Enforces dependency direction, keeps Domain framework-agnostic |
| CQRS | Mediator | Lighter-weight alternative to MediatR for command/query separation |
| Validation | FluentValidation | Declarative validation of commands/queries |
| Error handling | ErrorOr | Result-pattern error handling instead of exceptions for control flow |
| Relational data | PostgreSQL + EF Core | Users, quiz sets, game history — needs integrity and relations |
| Document data | MongoDB | Questions — flexible, nested structure per question type |
| In-memory data | Redis | Live game state, leaderboard, WebSocket connection tracking |
| Real-time | SignalR | Rooms, questions, answers, and leaderboard pushed live |
| Auth | ASP.NET Core Identity + JWT | Access + refresh token flow |
| Logging | Serilog | Structured logging |
| Docs | Scalar | Interactive API docs in Development, modern alternative to Swagger UI |
| Infra | Docker + docker-compose | One command spins up Postgres, Mongo, Redis, and the API together |

## 🚀 Quick start

```bash
git clone <repo-url>
cd QuizArena
cp .env.example .env.docker   # fill in JWT secret, DB passwords, etc.
docker compose up --build
```

This brings up PostgreSQL (`5432`), MongoDB (`27017`), Redis (`6379`), and the WebApi (`5000` → `8080` inside the container). Database migrations and the admin account seed run automatically on startup. In Development mode, interactive API docs are available via Scalar.

## 📁 Repository structure

```
src/
  backend/
    QuizArena.Domain/        — entities, enums, business rules, no dependencies
    QuizArena.Application/   — CQRS commands/queries, validators, interfaces
    QuizArena.Persistence/   — EF Core (Postgres), Mongo, Redis implementations
    QuizArena.WebApi/        — controllers, SignalR hub, DI, configuration
  frontend/                  — client app (in progress)
tests/
  backend/
    QuizArena.Domain.UnitTests/
    QuizArena.Application.UnitTests/
compose.yaml                 — orchestrates all services
```

## 📋 Project status / Roadmap

Being upfront about this so reviewers know exactly what to expect:

- ✅ **Done** — the entire backend: auth, quiz sets, full game logic, real-time via SignalR, admin panel, unit tests on the Domain and Application layers, Docker environment.
- 🚧 **In progress** — the frontend (built separately by a teammate), Telegram bot integration.
- 📋 **Planned** — integration tests for WebApi/SignalR, a CI/CD pipeline, and a more detailed API reference in the docs.

There's no production frontend or hosted demo yet — the backend is the focus of this project, and it's complete and playable via API/SignalR as-is.
