# Task: MVP Hardening — Foundations, UI, Workflow & Letters

**Start:** 2026-04-24 (ongoing across four plans)
**Branch:** demo/azure-deploy
**Worktree:** /Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai
**Plan:** docs/superpowers/plans/2026-04-24-foundations-identity.md (Plan 1 of 4)
**Contract docs in scope:** docs/contracts/auth-claims.md, docs/contracts/audit-event.md, docs/contracts/letter-context.md

**Acceptance criteria** (what "done" looks like for the whole MVP-hardening push):
- SystemAdmin can sign in, create other staff users with role + org unit, reset passwords, deactivate.
- Validator in Limpopo WMA sees only Limpopo cases; NationalManager sees all.
- Workflow transitions blocked unless guards satisfied; UI reports the blocking reason.
- S33(2) case skips CP5–CP9 and lands at S33_2_DeclarationIssued.
- RegionalManager can issue, sign, and download a Section 35 Letter 1 PDF.
- Every screen uses the wireframe shell and dws.css tokens; no Bootstrap colour utilities.
- Field labels render as English (SG Code, WARMS Registration Number, etc.).
- All unit + integration tests pass; existing 33 tests still green.

**Out of scope** (must not be touched):
- HDI indicator, property subdivide/consolidate flow, V&V case-number generator.
- Full X.509 signatures, SignaturePad.js UI, email/SMS notifications.
- External public-user portal, MFA.
- Full LawfulnessAssessmentService (GWCA rules + riparian rights + Section 9B).
- SAPWAT / dam / SFRA calculator engines, eWULAAS integration.
- Objection string sweep in views.

---

## Journal

> Each dispatched agent appends one entry. Read ALL prior entries before editing. Entries are terse bullets, file:line references, no narrative.

### <YYYY-MM-DD HH:MM> — <agent-name> — <one-line action>
- **Read:** <files>
- **Changed:** <files + brief>
- **Learned:** <what next agent should know>
- **Status:** DONE | DONE_WITH_CONCERNS | NEEDS_CONTEXT | BLOCKED
- **Concerns / context needed (if any):** <verbatim from agent's report>

### <next entry>

---

## Retro (fill in on task completion)

- **Converged:** <what landed cleanly>
- **Drifted:** <where agents disagreed or needed iteration>
- **Failed prompt pattern:** <specific phrasing to avoid in future>
- **Lesson worth promoting to agent memory?** <yes/no + which memory file to update>
