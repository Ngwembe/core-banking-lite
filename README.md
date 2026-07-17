# Refactoring Analysis

## Overview

This document details the changes made to the original `Demo` codebase, the reasoning behind each decision, the trade-offs accepted, and the remaining work.
The exercise involved taking a prototype withdrawal endpoint and evolving it into a production-grade, maintainable, and correct implementation.

---

## Table of Contents

1. [Critical Bugs Fixed](#1-critical-bugs-fixed)
2. [Security Issues Resolved](#2-security-issues-resolved)
3. [Resource & Performance Improvements](#3-resource--performance-improvements)
4. [Architectural Decisions](#4-architectural-decisions)
5. [Correctness & Data Integrity](#5-correctness--data-integrity)
6. [Observability](#6-observability)
7. [Testability](#7-testability)
8. [API Design](#8-api-design)
9. [Configuration & Dependency Management](#9-configuration--dependency-management)
10. [Trade-offs](#10-trade-offs)
11. [Known Remaining Issues](#11-known-remaining-issues)

---

## 1. Critical Bugs Fixed

### 1.1 Unreachable SNS Publishing Code (Silent Data Loss)

**Severity: Critical**

The original `Withdraw` method returned on every possible code path before the SNS publishing block was ever reached. Events were silently never published.

**Fix:** The Transactional Outbox pattern was introduced. The event is written atomically to the database in the same transaction as the balance deduction. 
A `BackgroundService` (`OutboxPublisherService`) reliably delivers it to SNS. Even if the process crashes mid-flight, the outbox row survives and is retried on restart — guaranteed at-least-once delivery.

---

### 1.2 TOCTOU Race Condition — Overdraft Vulnerability

**Severity: Critical**

The original code read the balance in one statement and wrote the deduction in a separate statement. Any two concurrent requests for the same account could both read the same balance, both pass the guard, and both deduct — causing an overdraft.

**Fix:** The guard and the deduction were collapsed into a single atomic SQLstatement. The `WHERE` clause IS the balance check — there is no gap between read and write.

---

### 1.3 `.Result` Deadlock Risk on Async Code

**Severity: High**

Blocking on `Task.Result` inside an ASP.NET request context consumes a thread pool thread and can deadlock the server under load.

**Fix:** All I/O is now fully `async`/`await` through every layer. The outbox background service handles SNS delivery outside the request pipeline entirely.

---

### 1.4 `rows == 0` Failure Ambiguity

**Severity: Medium**

After the atomic `UPDATE`, `rows == 0` collapsed three distinct failure causes into one undifferentiated error — making it impossible for API consumers or fraud systems to act correctly.

**Fix:** A single-statement `CASE` diagnostic query runs on the same connection after a failed `UPDATE`, classifying the cause without reintroducing a TOCTOU window (no write depends on this read):



This maps to distinct HTTP responses: `404`, `422`, and `400`.

---

## 2. Security Issues Resolved

### 2.1 Hard-coded Secrets in Source Code

**Fix:** All configuration is bound from `appsettings.json` and overridden by environment variables at runtime via `AddEnvironmentVariables()`. Secrets are never in code — they are injected by the hosting platform (ECS task definition, Kubernetes Secret, etc.), satisfying 12-factor app principles.

---

### 2.2 Floating-Point Money Storage

**Severity: High**

SQLite's `REAL` type is an IEEE 754 64-bit float. It cannot represent most decimal fractions exactly. Stored over thousands of transactions, sub-cent rounding errors accumulate silently.

**Fix:** Balances are stored as `INTEGER` minor currency units (cents/pence). Integer arithmetic in SQLite is exact up to 64-bit signed range. Conversion happens at the application boundary:

---

## 3. Resource & Performance Improvements

### 3.1 AWS SDK Client Lifecycle

**Fix:** `IAmazonSimpleNotificationService` is registered as a singleton in DI. The SDK client is designed for shared use; connection pooling is preserved across all requests.

### 3.2 Configurable Outbox Batch Size

The original hardcoded `LIMIT 10` in SQL. The batch size is now bound to `InfrastructureOptions.OutboxBatchSize` (defaulting to `10`) and can be tuned per environment without a code change:

---

## 4. Architectural Decisions

### 4.1 Transactional Outbox Pattern

**Problem:** Publishing to SNS inside a web request is unreliable. If SNS is unavailable or the process crashes after the DB write but before the publish, the event is lost permanently.

**Decision:** Events are written to an `outbox_messages` table **in the same database transaction** as the balance deduction. A `BackgroundService` polls the outbox and delivers to SNS independently. If delivery fails, the row remains unprocessed and is retried on the next cycle.

**Trade-off:** Introduces eventual consistency — the SNS event arrives slightly after the HTTP response. This is the correct trade-off for financial systems: durability over immediacy.


### 4.2 Repository Pattern + Interface Segregation

**Decision:** All data access is behind `IBankAccountRepository` and `IOutboxRepository`. The controller and `OutboxPublisherService` depend only on interfaces.

**Trade-off:** Adds indirection. For a two-table SQLite demo this feels like over-engineering. The justification is that the persistence layer was already identified as the likely swap target (SQLite → SQL Server / EF Core), so the seam is necessary.

### 4.3 Persistence Swap Seam via Extension Methods


All SQLite-specific wiring — keep-alive connection, repository registrations, outbox repository — lives in `PersistenceServiceCollectionExtensions`. No other file needs to change when the provider is swapped.

### 4.4 Railway-Oriented Error Handling (`Result<T>`)

**Decision:** Instead of exceptions for expected business failures (insufficient funds, invalid input), a `Result<T>` type carries either a value or an error string. The controller pipeline uses `BindAsync` / `MatchAsync` to chain steps:

**Trade-off:** Unfamiliar to engineers who have not worked with functional patterns. The benefit is that failure handling is explicit and cannot be accidentally omitted — the compiler enforces it.

### 4.5 Environment-Aware Publisher


`FakeSnsPublisher` logs to the console instead of calling AWS. Developers can run the full application locally without AWS credentials or mocking at the test level.

---

## 5. Correctness & Data Integrity

### 5.1 Transactional Atomicity for Outbox

The balance deduction and outbox insert execute inside a single `BeginTransactionAsync` block. If the outbox insert fails, the deduction rolls back — no money leaves the account without a corresponding event record. If the deduction fails, no phantom event is written.

### 5.2 HTTP Verb Semantics

| Operation | Original | Fixed |
|---|---|---|
| Withdraw | Any verb | `POST` (state mutation) |
| Get balance | Not present | `GET` (idempotent read) |

### 5.3 Input Validation Before DB Round-Trip

`accountId <= 0` and `amount <= 0` are rejected in `ValidateInput` before any database connection is opened, avoiding unnecessary I/O.

---

## 6. Observability

The original had zero logging. The improved version uses structured log properties throughout:


Named placeholders are indexed as discrete fields by structured logging sinks — not string-concatenated — enabling queries like
`WHERE EventType = 'WithdrawalEvent' AND Id = 'abc-123'`.

---

## 7. Testability

| Concern | Original | Fixed |
|---|---|---|
| SNS | Concrete `AmazonSNSClient` in constructor | `ISnsPublisher` — mockable |
| Data access | Raw `IDbConnection` ADO.NET | `IBankAccountRepository`, `IOutboxRepository` |
| Outbox delivery | Inline in controller | `IOutboxRepository` — mockable |
| Dev environment | Real AWS required | `FakeSnsPublisher` — no credentials |
| Business logic isolation | Impossible | Controller testable with mock repository |

---

## 8. API Design

| | Original | Fixed |
|---|---|---|
| Return type | `string` | `IActionResult` |
| Success status | `200 OK` always | `200 OK` |
| Validation error | `200 OK` with string | `400 Bad Request` |
| Not found | `200 OK` with string | `404 Not Found` |
| Business rule failure | `200 OK` with string | `422 Unprocessable Entity` |
| Response body | Plain string | Structured JSON `{ "error": "..." }` |
| Route convention | `/bank/withdraw` | `/api/bank/withdraw` |

---

## 9. Configuration & Dependency Management

### Environment-Layered Configuration


All configuration is accessed via `IOptions<InfrastructureOptions>` — no raw `IConfiguration` string indexing throughout the application code.

---

## 10. Trade-offs

| Decision | Benefit | Cost |
|---|---|---|
| Transactional Outbox | Guaranteed event delivery, no data loss | Eventual consistency on SNS; background polling overhead |
| `Result<T>` monad | Explicit, composable error handling | Functional pattern unfamiliar to some engineers |
| SQLite in-memory | Zero infrastructure for local dev | Shared-memory keep-alive connection required; not suitable for multi-instance deployment |
| Integer minor-unit storage | Exact decimal arithmetic | Conversion layer required at every DB boundary; consumer-visible values are in minor units |
| `IOutboxRepository` abstraction | Outbox swappable independently of account repo | Extra interface + class for what is currently two SQL statements |
| `FakeSnsPublisher` | No AWS credentials needed in dev | Fake must be kept behaviourally consistent with real publisher |

---

## 11. Known Remaining Issues

| # | Issue | Priority | Mitigation |
|---|---|---|---|
| 1 | No unit or integration tests despite full testability infrastructure | High | Out of scope |
| 2 | `WithdrawalEvent.ToJson()` — serialization logic on the model violates SRP | Low | Move serialization logic to a dedicated service |
| 3 | Failure reasons are still `string` — controller maps HTTP status via substring match; a typed `enum` would be safer | Low | Could consider introducing a typed `enum` for failure reasons |