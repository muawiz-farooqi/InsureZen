# Requirements Analysis

**Project:** InsureZen Backend REST APIs  
**Assessment Task:** Task 1 - Requirements Analysis  
**Author:** Muhammad Muawiz  
**Date:** 21 April 2026  

This document provides a complete requirements breakdown for the InsureZen medical insurance claim processing system as described in the assessment brief. It covers entities, actors, functional and non-functional requirements, edge cases, constraints, and explicit assumptions.

## 1. Entities and Data Points

### Core Entities

1. **Claim** (central entity)
   - `claimId` (string, UUID or sequential identifier – unique across the system)
   - `externalClaimReference` (string – original claim number from the insurance company)
   - `insuranceCompany` (string or object – name/code of the partner insurance company)
   - `submissionDate` (datetime – when the claim was received by InsureZen)
   - `standardizedData` (object – normalized extracted fields from the upstream OCR service)
     - Patient information: `patientName`, `patientDateOfBirth`, `policyNumber`
     - Claim details: `claimAmount`, `diagnosisCodes` (array), `procedureCodes` (array), `serviceDate`, `providerName`
     - Other relevant fields: `totalBilledAmount`, `insuranceCoveredAmount`, `patientResponsibility`
   - `status` (enum – see state transitions below)
   - `makerId` (string – ID of the Maker who reviewed the claim)
   - `makerFeedback` (string – optional notes/annotations from the Maker)
   - `makerRecommendation` (`Recommendation` enum: `Approve` | `Reject` | `null`)
   - `makerReviewedAt` (datetime)
   - `checkerId` (string – ID of the Checker who reviewed the claim)
   - `checkerDecision` (`Recommendation` enum: `Approve` | `Reject` | `null`)
   - `checkerFeedback` (string – optional notes from the Checker)
   - `checkerReviewedAt` (datetime)
   - `forwardedAt` (datetime – when the claim was forwarded to the insurance company)
   - `forwardedTo` (string – insurance company reference)
   - `createdAt`, `updatedAt` (audit timestamps)

2. **ClaimStatus** (enum – represents the state machine)
   - `NEW` (ingested, awaiting Maker)
   - `MAKER_ASSIGNED` (assigned to a Maker)
   - `MAKER_REVIEWED` (Maker has submitted recommendation)
   - `CHECKER_ASSIGNED` (assigned to a Checker)
   - `COMPLETED` (Checker has issued final decision)
   - `FORWARDED` (record sent to insurance company)

3. **Employee** (abstract – Makers and Checkers)
   - `employeeId` (string)
   - `role` (enum: `Maker` | `Checker`)
   - `name` (string)

4. **AuditLog** (for traceability)
   - `logId`, `claimId`, `actorId`, `action`, `timestamp`, `details`

### Standardized Input Representation
The upstream service will POST a JSON payload conforming to the `Claim` entity (excluding review fields). This is the contract we define and expect.

## 2. Actors and Roles

| Actor                  | Role                                                                 | System Interaction                     |
|------------------------|----------------------------------------------------------------------|----------------------------------------|
| **Upstream OCR Service** | Provides standardized claim data after extraction                    | POST `/claims` (ingestion)            |
| **Maker**              | Reviews extracted data, adds feedback, submits Approve/Reject recommendation | GET available claims, POST review     |
| **Checker**            | Independently reviews claim + Maker’s recommendation & feedback, issues final decision | GET reviewed claims, POST final decision |
| **Frontend Dashboard** | Displays analytical summaries and searchable/paginated claim history | GET `/claims/history`, GET summaries  |
| **Insurance Company**  | Receives final claim record (no direct API interaction required)    | Internal forwarding stub only         |

**Note:** Makers and Checkers are InsureZen employees operating concurrently. No customer/claimant actor interacts directly with the system.

## 3. Functional Requirements

1. **Claim Ingestion**
   - Accept standardized claim data from upstream service.
   - Create a new `Claim` record with initial status `New`.

2. **Maker Flow**
   - Retrieve list of claims available for review (`New` or `InMakerReview`).
   - Assign a claim to a Maker (concurrent-safe).
   - Submit Maker review (feedback + recommendation).

3. **Checker Flow**
   - Retrieve list of claims ready for checking (`MakerReviewed` or `InCheckerReview`).
   - Submit final decision (Approve/Reject) + optional feedback.

4. **Claim Forwarding**
   - When Checker decision is recorded, transition status to `Completed` → `Forwarded`.
   - Log the forwarding action (stub – no real external integration required).

5. **Claim History & Search**
   - Provide a paginated, filterable list of all claims.
   - Supported filters: status, insurance company, date range (submission/review/forwarded), claim amount range.

6. **Analytical Summaries** (implied by frontend)
   - Endpoints to support dashboard metrics (e.g., total claims, approval rate, average processing time, claims by company).

7. **State Transitions**
   - Enforce valid progression: New → MakerReview → CheckerReview → Completed → Forwarded.
   - Prevent invalid actions (e.g., Checker reviewing before Maker).

## 4. Non-Functional Requirements

- **Concurrency & Scalability**: System must safely handle hundreds to thousands of claims per day with multiple Makers/Checkers working simultaneously. No two Makers should be able to review the same claim at the same time.
- **Data Integrity**: Claim state must always be consistent. All transitions must be atomic.
- **Auditability**: Every change (ingestion, review, decision, forwarding) must be traceable with who, what, and when.
- **Performance**: Paginated endpoints must be efficient (indexing, proper query design). Response times < 200ms for typical operations.
- **Error Handling**: Clear, consistent error responses with meaningful messages and appropriate HTTP status codes.
- **Input Validation**: All incoming data must be validated (required fields, data types, business rules).
- **Idempotency & Resilience**: Ingestion and state updates should be idempotent where appropriate.
- **Observability**: Logging and error tracking should be sufficient for production debugging.

## 5. Edge Cases and Constraints

### Identified Edge Cases
- Concurrent Maker assignment (race condition).
- Maker or Checker attempting to review a claim already in another state.
- Claim ingestion with missing or invalid standardized data.
- Checker decision before Maker recommendation (should be blocked).
- Very large claim volumes (pagination must prevent full table scans).
- Duplicate external claim references from different insurance companies.
- Claims that are rejected at Maker stage (still proceed to Checker per workflow).
- Long-running reviews (claims left in `InMakerReview` or `InCheckerReview` for extended periods).
- Filtering with no results or extreme date ranges.
- Partial/incomplete feedback submissions.

### Constraints
- No OCR/document parsing required.
- No real external communication with insurance companies (stub sufficient).
- Must support concurrent operations without data corruption.
- Frontend expects specific response shapes (must be consistent and well-documented).

## 6. Assumptions and Justifications

| Assumption | Justification |
|------------|---------------|
| Claim uses a strict linear state machine with 6 states | Ensures data integrity and supports safe concurrent operations |
| Makers and Checkers are identified only by `employeeId` | Authentication and full user management is a Tier-2 bonus task |
| Concurrency is handled with optimistic locking or database constraints | Meets the explicit requirement without over-engineering for junior level |
| Forwarding is represented by a status change plus log entry | Problem states a stub or logged record of the forwarding action is sufficient |
| Analytical summary endpoints are part of the API | Frontend dashboard is stated to consume these for metrics |
| PostgreSQL is used as the database | Recommended in Tier-1 bonus and provides excellent concurrency support |
| All timestamps are stored in UTC | Standard practice for distributed systems |
| Soft deletes are not implemented | Not mentioned anywhere in the problem statement |
| Pagination and basic filtering are implemented with database indexes | Required for good performance when handling hundreds to thousands of claims per day. |

All assumptions will be clearly documented in the README.md