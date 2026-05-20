# Task: Bug-fix sprint — all review findings
**Start:** 2026-05-19T18:15
**Branch:** demo/azure-deploy
**Acceptance criteria:**
- All 9 code bugs fixed (Critical: 1, 2; Important: 3–7; Security: S1, S2)
- A3 (LastCalculatedAt stamp), A4 (WorkflowService refactor), A5 (SfraResult property-init) done
- dotnet build: 0 errors
- dotnet test: ≥210 passing, no regressions
- Missing tests added (guards, GetBlockingReasonsAsync, PAJA gate, calculator edge cases)

## File ownership per agent (non-overlapping)
- agent-calculator : Services/Calculator/*.cs, Models/FieldAndCrop.cs, Models/DamCalculation.cs, Models/Forestation.cs, new migration
- agent-filemaster  : Controllers/FileMasterController.cs, Services/Letters/LetterService.cs (SignedByUserId fix only)
- agent-controllers : Controllers/DamCalculationController.cs, Controllers/FieldAndCropController.cs, Controllers/ForestationController.cs, Views/Forestation/Create.cshtml
- agent-workflow    : Services/WorkflowService.cs
- agent-tests       : Tests/** (new files only, runs after all 4 above)

## Journal

### 2026-05-19T18:15 — orchestrator — task setup
- Created journal; dispatched 4 parallel agents (calculator, filemaster, controllers, workflow)
- Tests agent queued for Batch 2 after all Batch 1 commits

