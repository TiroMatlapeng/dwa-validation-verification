# AuditEvent Contract

**Owners:** dotnet-architect (producer and consumer today; eWULAAS integration becomes a second consumer later)

## Shape

An `AuditEvent` is the structured payload that `IAuditService.LogAsync` accepts. It maps onto an `AuditLog` row (already existing entity) using the following field correspondence:

| AuditEvent field | Type | Maps to `AuditLog` column | Purpose |
|---|---|---|---|
| `EntityType` | string | `EntityType` | The domain entity class name (e.g. `FileMaster`, `ApplicationUser`, `LetterIssuance`). |
| `EntityId` | string | `EntityId` | The primary key of the entity, as a string (Guids → `ToString()`). |
| `Action` | string | `Action` | Short verb phrase (e.g. `WorkflowAdvanced`, `UserCreated`, `LetterIssued`). |
| `UserId` | Guid? | `ApplicationUserId` | The signed-in user, or null for system-initiated actions. |
| `UserDisplayName` | string? | `UserName` | Human-readable actor name at the time of the event. |
| `FromValue` | string? | `OldValues` (JSON-wrapped) | Prior state, e.g. previous workflow state name. |
| `ToValue` | string? | `NewValues` (JSON-wrapped) | New state, e.g. target workflow state name. |
| `Reason` | string? | `Description` | Human-written justification; may contain notes. |
| `IPAddress` | string? | `IPAddress` | Source IP if available from the HTTP context. |
| `OccurredAt` | DateTime | `Timestamp` | UTC. |

## Fixture

`contracts/fixtures/audit/audit-event.json` — canonical example for a workflow advance from CP1 to CP2 by a Validator.

## Producer

`Services/Audit/AuditService.cs` — implements `IAuditService.LogAsync(AuditEvent)`, writes one immutable row to `AuditLog`. Never updates or deletes existing rows.

## Consumers

- `Views/FileMaster/Details.cshtml` — Audit tab lists audit rows for the case, reverse chronological.
- `Services/WorkflowService.cs` — emits audit events on every state transition.
- `Controllers/Admin/UsersController.cs` — emits audit events on user create, edit, password reset, deactivate, reactivate.
- (Future) Any outbound eWULAAS export pipeline.

## Invariants

- `EntityId` is the stringified primary key of a real entity; never empty.
- `Action` uses PascalCase verb phrases; never whitespace-free but also never multi-sentence.
- `FromValue` and `ToValue` are either both null (creation events) or both set (transition events).
- `OccurredAt` is UTC. Producer MUST call `DateTime.UtcNow`, never `DateTime.Now`.
- Audit rows are append-only; no service or controller ever updates or deletes an `AuditLog` row.

## How to change this contract

1. Update `docs/contracts/audit-event.md` and `contracts/fixtures/audit/audit-event.json` in the same commit.
2. Two-agent review (backend producer + future eWULAAS consumer) on the diff.
3. Update `AuditService` + at least one consumer's test against the fixture.
4. Append a one-liner to `docs/contracts/CHANGELOG.md`.
