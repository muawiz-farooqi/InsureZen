# Requirements Analysis

**Project:** InsureZen Backend REST APIs  
**Assessment Task:** Task 1 - Requirements Analysis  
**Author:** Muhammad Muawiz  
**Date:** 21 April 2026  

This document provides a complete requirements breakdown for the InsureZen medical insurance claim processing system as described in the assessment brief. It covers entities, actors, functional and non-functional requirements, edge cases, constraints, and explicit assumptions.

---

## 0. Tech Stack

| Concern | Choice |
|---|---|
| **Language / Framework** | C# · ASP.NET Core Web API |
| **ORM** | Entity Framework Core |
| **Database** | PostgreSQL |
| **Validation** | FluentValidation |
| **Containerisation** | Docker + docker-compose |

---

## 1. Entities and Data Points

### Core Entities

1. **Claim** (central entity)
   - `claimId` (Guid – primary key, generated on ingestion)
   - `externalClaimReference` (string – original claim number from the insurance company)
   - `insuranceCompany` (string – name/code of the partner insurance company)
   - `submissionDate` (datetime – when the claim was received by InsureZen)
   - `standardizedData` (**JSON column** – normalized extracted fields from the upstream OCR service; stored as a flexible JSON blob to avoid schema churn)
     - Patient information: `patientName`, `patientDateOfBirth`, `policyNumber`
     - Claim details: `claimAmount`, `diagnosisCodes` (array), `procedureCodes` (array), `serviceDate`, `providerName`
     - Financial fields: `totalBilledAmount`, `insuranceCoveredAmount`, `patientResponsibility`
   - `status` (enum – `ClaimStatus`, see state transitions below)
   - `assignedTo` (string – employee ID of whoever currently holds the claim; applies to both Maker and Checker stages)
   - `assignedAt` (datetime – when the current assignment occurred)
   - `rowVersion` / `xmin` (byte[] – EF Core concurrency token; prevents two workers updating the same row simultaneously)
   - `makerId` (string – ID of the Maker who completed review)
   - `makerFeedback` (string – optional notes/annotations from the Maker)
   - `makerRecommendation` (`Recommendation` enum: `APPROVE` | `REJECT` | `null`)
   - `makerReviewedAt` (datetime)
   - `checkerId` (string – ID of the Checker who completed review)
   - `checkerDecision` (`Recommendation` enum: `APPROVE` | `REJECT` | `null`)
   - `checkerFeedback` (string – optional notes from the Checker)
   - `checkerReviewedAt` (datetime)
   - `forwardedAt` (datetime – when the claim was forwarded to the insurance company)
   - `forwardedTo` (string – insurance company reference)
   - `createdAt`, `updatedAt` (audit timestamps – UTC)

2. **ClaimStatus** (enum – represents the state machine)
   - `NEW` → ingested, awaiting Maker assignment
   - `MAKER_ASSIGNED` → locked to a Maker
   - `MAKER_REVIEWED` → Maker has submitted recommendation
   - `CHECKER_ASSIGNED` → locked to a Checker
   - `COMPLETED` → Checker has issued final decision
   - `FORWARDED` → record forwarded to insurance company (terminal state)

3. **Employee** (abstract – Makers and Checkers)
   - `employeeId` (string)
   - `role` (enum: `Maker` | `Checker`)
   - `name` (string)

4. **AuditLog** (for traceability)
   - `logId`, `claimId`, `actorId`, `action`, `timestamp`, `details`

### Standardized Input Representation
The upstream OCR service POSTs a JSON payload. The `standardizedData` object is persisted as a single JSON column, keeping the schema stable even if the OCR output evolves.

---

## 2. Actors and Roles

| Actor | Role | System Interaction |
|---|---|---|
| **Upstream OCR Service** | Provides standardized claim data after extraction | `POST /api/claims` |
| **Maker** | Reviews extracted data, adds feedback, submits Approve/Reject recommendation | `GET /api/claims/available/maker`, `PATCH /api/claims/{id}/maker-assign`, `PATCH /api/claims/{id}/maker-review` |
| **Checker** | Independently reviews claim + Maker recommendation, issues final decision | `GET /api/claims/available/checker`, `PATCH /api/claims/{id}/checker-assign`, `PATCH /api/claims/{id}/checker-review` |
| **Frontend Dashboard** | Displays analytical summaries and searchable/paginated claim history | `GET /api/claims`, `GET /api/claims/history` |
| **Insurance Company** | Receives final claim record | Internal forwarding stub only |

**Note:** Makers and Checkers are InsureZen employees operating concurrently. No customer/claimant actor interacts directly with the system.

---

## 3. Functional Requirements

1. **Claim Ingestion**
   - Accept standardized claim data from upstream service via `POST /api/claims`.
   - Persist `standardizedData` as a JSON column.
   - Create a new `Claim` record with initial status `NEW`. Return `201 Created` with the full `ClaimDto`.

2. **Maker Flow**
   - `GET /api/claims/available/maker` – returns claims with status `NEW`.
   - `PATCH /api/claims/{id}/maker-assign` – atomically assigns the claim to a Maker; transitions status to `MAKER_ASSIGNED`. Returns `409 Conflict` if already assigned.
   - `PATCH /api/claims/{id}/maker-review` – accepts `recommendation` + optional `makerFeedback`; transitions status to `MAKER_REVIEWED`.

3. **Checker Flow**
   - `GET /api/claims/available/checker` – returns claims with status `MAKER_REVIEWED`.
   - `PATCH /api/claims/{id}/checker-assign` – atomically assigns the claim to a Checker; transitions status to `CHECKER_ASSIGNED`. Returns `409 Conflict` if already assigned.
   - `PATCH /api/claims/{id}/checker-review` – accepts `decision` + optional `checkerFeedback`; transitions status to `COMPLETED` then immediately `FORWARDED`; logs forwarding action (stub).

4. **Claim Forwarding**
   - Triggered automatically as part of `checker-review`. No separate endpoint needed.
   - Sets `forwardedAt`, `forwardedTo`, transitions to `FORWARDED`, and appends an `AuditLog` entry.

5. **Claim History & Search**
   - `GET /api/claims?status=&insuranceCompany=&startDate=&endDate=&page=1&size=20`
   - Paginated, filterable list of all claims.
   - Supported filters: `status`, `insuranceCompany`, date range (`startDate` / `endDate`).

6. **Analytical Summaries** *(implied by frontend)*
   - Endpoints supporting dashboard metrics: total claims, approval rate, average processing time, claims by company.

7. **State Machine Enforcement**
   - Valid progression only: `NEW → MAKER_ASSIGNED → MAKER_REVIEWED → CHECKER_ASSIGNED → COMPLETED → FORWARDED`.
   - Any attempt to skip or reverse a state returns `400 Bad Request` with a descriptive message.

---

## 4. Non-Functional Requirements

- **Concurrency & Scalability**: Multiple Makers/Checkers operate simultaneously. Assignment endpoints must be race-condition-safe. Implementation options:
  - *Optimistic*: EF Core `RowVersion` / PostgreSQL `xmin` – `DbUpdateConcurrencyException` → `409`.
  - *Pessimistic*: `SELECT … FOR UPDATE SKIP LOCKED` in a transaction.
  - *In-memory fallback*: `SemaphoreSlim` per claim ID (Phase 1 only).
- **Data Integrity**: All status transitions are atomic (single database transaction).
- **Auditability**: Every change must record actor ID, action, and UTC timestamp in `AuditLog`.
- **Performance**: Paginated queries use indexed columns (`status`, `insuranceCompany`, `submissionDate`). Target < 200 ms for typical reads.
- **Error Handling**: Consistent `ProblemDetails`-style responses with meaningful messages and correct HTTP status codes (200, 201, 400, 404, 409).
- **Input Validation**: FluentValidation rules on all request DTOs (required fields, data types, business rules).
- **Idempotency**: Re-submitting the same ingestion payload should be detectable via `externalClaimReference`.
- **Observability**: Structured logging (e.g., Serilog) and EF Core query logging in development mode.

---

## 5. Edge Cases and Constraints

### Identified Edge Cases
- Concurrent Maker/Checker assignment race condition (two workers grab the same claim simultaneously).
- Actor attempting a transition on a claim already in an incompatible state (e.g., double-assign).
- Claim ingestion with missing or malformed `standardizedData`.
- Checker attempting `checker-review` before a Maker has completed `maker-review`.
- Very large claim volumes – pagination prevents full table scans; indexes are mandatory.
- Duplicate `externalClaimReference` values from the same insurance company.
- Claims rejected by Maker still proceed to Checker (rejection is a recommendation, not a block).
- Stale claims stuck in `MAKER_ASSIGNED` / `CHECKER_ASSIGNED` (out of scope for MVP; mention in README).
- Filtering with no results or extreme date ranges (must return empty paginated response, not an error).
- Concurrency token mismatch (`RowVersion` conflict) must surface as `409`, not `500`.

### Constraints
- No OCR/document parsing required.
- No real external communication with insurance companies (stub sufficient).
- Must support concurrent operations without data corruption.
- Frontend expects consistent, well-documented response shapes.

---

## 6. Assumptions and Justifications

| Assumption | Justification |
|---|---|
| Claim uses a strict linear state machine with 6 states | Ensures data integrity and supports safe concurrent operations |
| `standardizedData` is stored as a JSON column | Keeps the schema flexible if the OCR output schema evolves |
| `assignedTo` + `assignedAt` fields added to Claim | Required for locking semantics during Maker/Checker assignment |
| `RowVersion` / `xmin` added as EF Core concurrency token | Standard EF Core pattern; maps cleanly to PostgreSQL `xmin` |
| Makers and Checkers are identified only by `employeeId` | Full auth/user management is a Tier-2 bonus task |
| Concurrency handled with EF Core optimistic concurrency (Phase 2) | Clean .NET idiom; falls back to in-memory `SemaphoreSlim` in Phase 1 |
| Assignment (`maker-assign` / `checker-assign`) uses `PATCH`, not `POST` | Partial update of an existing resource; RESTfully correct |
| Review (`maker-review` / `checker-review`) also uses `PATCH` | Same rationale as above |
| Forwarding is an automatic side-effect of `checker-review` | No separate forwarding endpoint needed; simplifies the client |
| Analytical summary endpoints exist as part of the API | Frontend dashboard is stated to consume these for metrics |
| PostgreSQL is the production database; SQLite allowed for local dev | Postgres supports `SELECT … FOR UPDATE` and `xmin` natively |
| All timestamps stored in UTC | Standard practice for distributed systems |
| Soft deletes are not implemented | Not mentioned in the problem statement |
| Pagination uses database indexes on `status`, `insuranceCompany`, `submissionDate` | Required for performance at scale |

All assumptions are documented here and will be summarised in `README.md`.