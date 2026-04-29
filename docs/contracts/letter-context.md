# LetterContext Contract

**Owners:** dotnet-architect (producer `LetterService.BuildContext`), dotnet-architect (consumers = all `ILetterTemplate` implementations)

## Shape

`LetterContext` is a strongly-typed DTO passed from `LetterService` to every `ILetterTemplate`. Templates read fields from this context and compose them into the PDF document tree. All fields are present on every render (never null except where explicitly optional).

| Field | Type | Required | Notes |
|---|---|---|---|
| `ReferenceNumber` | string | yes | Human-readable unique ID for the letter (e.g. `VV-LIM-2026-0001-L1`). |
| `IssueDate` | DateOnly | yes | Date the letter is being issued. UTC-derived. |
| `DueDate` | DateOnly? | no | Statutory response due date (60 days default for S35 letters). |
| `CaseNumber` | string | yes | FileMaster V&V case number or WARMS registration number (fallback). |
| `FarmName` | string | yes | Human-friendly farm descriptor for the salutation. |
| `PropertyReference` | string | yes | SG Code or PropertyReferenceNumber. |
| `RecipientName` | string | yes | Primary property owner / water user being addressed. |
| `RecipientAddress` | string? | no | Postal address when available. |
| `IrrigationBoardName` | string? | no | Required for S33(2) declarations; null otherwise. |
| `SignatoryName` | string | yes | Person signing (usually RegionalManager). |
| `SignatoryTitle` | string | yes | Their role/title. |
| `SignatoryOrgUnit` | string | yes | Regional office they represent. |
| `LawfulVolumeM3` | decimal? | no | ELU volume for Letter 3 / declarations. |
| `UnlawfulVolumeM3` | decimal? | no | Unlawful portion for Letter 4A/5. |
| `AdditionalNotes` | string? | no | Free-form notes captured at issue time. |

## Fixture

`contracts/fixtures/letters/letter-context.json` — canonical example for a S35 Letter 1 being issued to a Limpopo farm.

## Producer

`Services/Letters/LetterService.cs` — `BuildContextAsync(fileMasterId, letterTypeCode, issuedBy)` loads related entities (FileMaster → Property → WMA → OrgUnit, PropertyOwnership → PropertyOwner, optional IrrigationBoard, signatory ApplicationUser) and projects onto a `LetterContext`.

## Consumers

`Services/Letters/Templates/*.cs` — every `ILetterTemplate` receives `LetterContext` in its `Compose(IContainer, LetterContext)` method. The three templates shipped in this iteration:
- `S35Letter1Template` — Notice to apply for verification (S35(1))
- `S35Letter3Template` — Confirmation of ELU (S35(4)) — ELU certificate
- `S33_2DeclarationTemplate` — Kader Asmal scheduled-area declaration

## Invariants

- `ReferenceNumber` is unique across all issued letters; never reused.
- `IssueDate` ≤ `DueDate` when both are set.
- `SignatoryName` / `SignatoryTitle` / `SignatoryOrgUnit` are captured at issue time (not resolved at render time) so the PDF remains stable even after staff org changes.
- `IrrigationBoardName` is populated only for `S33_2DeclarationTemplate` consumers; other templates ignore it.
- Every template MUST handle the case where optional fields are null (no NullReferenceException on render).

## How to change this contract

1. Update `docs/contracts/letter-context.md` + `contracts/fixtures/letters/letter-context.json` in the same commit.
2. Two-agent review (service author + template author) on the diff.
3. Update `LetterService.BuildContextAsync` to populate any new field and at least one template to consume it.
4. Append CHANGELOG line.
