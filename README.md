# InsureZen Claims API

Backend REST API for **InsureZen** medical claim intake and a **Maker → Checker** review workflow. Upstream services send normalized claim payloads; this service persists them, enforces a linear state machine, and records forwarding to insurers as a stub (no real outbound integration).

This repository is a submission for the **Tech Unicorn .NET Backend Engineer** assessment. Task deliverables are mapped below so reviewers can find them quickly.

## Assessment deliverables

| Task | Deliverable | Where |
|------|-------------|--------|
| **1 – Requirements analysis** | Entities, actors, functional/non-functional requirements, edge cases, constraints, assumptions | [`REQUIREMENTS.md`](REQUIREMENTS.md) (full analysis; avoids duplicating long tables in this README) |
| **2 – API design** | Endpoints, methods, bodies, status codes, Maker/Checker flows, forwarding model | [`openapi.yaml`](openapi.yaml) |
| **3 – Implementation** | Maker/Checker flows, paginated filterable history, validation, concurrency-safe transitions, forwarding stub | `src/` |

**Assumptions:** The brief asks assumptions to be documented for interview justification. The authoritative list with rationale is in the **Assumptions and Justifications** section of [`REQUIREMENTS.md`](REQUIREMENTS.md). At a high level: strict linear `ClaimStatus` state machine; `standardizedData` stored as JSON for OCR schema drift; Makers/Checkers identified by string IDs (no IdP in scope); forwarding runs automatically after Checker review; optimistic concurrency via `rowVersion` and the `If-Match` header on mutating `PATCH` calls.

## Tech stack

- **.NET** 10 (ASP.NET Core Web API)
- **PostgreSQL** 16 (Npgsql + EF Core)
- **FluentValidation**, **Swagger** (Swashbuckle)
- **Docker** / **docker-compose**

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (recommended), **or**
- [.NET 10 SDK](https://dotnet.microsoft.com/download) and a running PostgreSQL instance you can point the app at.

## Run with Docker (primary)

From the repository root:

```powershell
docker compose up --build
```

- **API:** http://localhost:8080  
- **Swagger UI:** http://localhost:8080/swagger (Development environment; enabled in the container via `ASPNETCORE_ENVIRONMENT=Development` in [`docker-compose.yml`](docker-compose.yml))  
- **Database:** PostgreSQL on the internal `db` service; the API applies EF migrations on startup.

To stop: `Ctrl+C`, then optionally `docker compose down` (add `-v` to remove the named volume and reset the DB).

## Run locally (without Docker)

1. Create a PostgreSQL database (e.g. `insurezen`) and user.
2. Set `ConnectionStrings:DefaultConnection` in [`src/appsettings.json`](src/appsettings.json) or via environment variable `ConnectionStrings__DefaultConnection` to your Npgsql connection string.
3. From the repo root:

```powershell
dotnet run --project src/InsureZen.csproj
```

Migrations run automatically on startup (`Program.cs`). Use the same Swagger URL if `ASPNETCORE_ENVIRONMENT` is `Development`.

## Tests

```powershell
dotnet test src/Tests/InsureZen.Tests.csproj
```

Covers claim state transitions and related behavior.

## API overview

Base route: **`/api/claims`**. Full contracts, schemas, and status codes: **[`openapi.yaml`](openapi.yaml)**.

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/api/claims` | Ingest claim (upstream OCR / normalized payload) |
| `GET` | `/api/claims`, `/api/claims/history` | Paginated list; query: `page`, `size`, `status`, `insuranceCompany`, `startDate`, `endDate` |
| `GET` | `/api/claims/{claimId}` | Single claim |
| `GET` | `/api/claims/available/maker` | Claims in `NEW` |
| `GET` | `/api/claims/available/checker` | Claims in `MAKER_REVIEWED` |
| `PATCH` | `/api/claims/{claimId}/maker-assign` | Assign Maker (`MAKER_ASSIGNED`) |
| `PATCH` | `/api/claims/{claimId}/maker-review` | Maker recommendation + feedback |
| `PATCH` | `/api/claims/{claimId}/checker-assign` | Assign Checker (`CHECKER_ASSIGNED`) |
| `PATCH` | `/api/claims/{claimId}/checker-review` | Checker decision + feedback; then forwarded (stub) |

**Concurrency:** Assignment and review `PATCH` operations expect the **`If-Match`** header with the current **`rowVersion`** (Base64) from the last `ClaimDto`. Stale versions return **409 Conflict** alongside other conflict cases (e.g. already assigned).

**State order:** `NEW` → `MAKER_ASSIGNED` → `MAKER_REVIEWED` → `CHECKER_ASSIGNED` → `COMPLETED` → `FORWARDED`. Invalid skips return **400 Bad Request**.

## Project layout

- `src/` – Web API, EF Core `AppDbContext`, services, validators, migrations  
- `src/Tests/` – xUnit tests  
- `openapi.yaml` – OpenAPI 3 specification (Task 2)  
- `REQUIREMENTS.md` – Requirements analysis (Task 1)

## Bonus

- **Tier 1:** PostgreSQL, Docker + `docker compose`, automated tests — included.  
- **Tier 2:** Sequence diagrams, split microservices, JWT/IdP RBAC — not included in this submission.

---

*AI tools may have been used as a productivity aid; architecture and trade-offs are documented in `REQUIREMENTS.md` for review discussion.*
