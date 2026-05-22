# Task: Medium Priority Bug Sprint — BUG-009, 016, 017, 020, 021, MISSING-001
**Start:** 2026-05-22T08:00+02:00
**Branch:** main
**Worktree:** /Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai
**Plan:** N/A (inline fixes)

**Acceptance criteria:**
- BUG-009: HomeController queries DB for all 5 KPI values; dashboard no longer shows hardcoded 11 March 2026 date or static numbers
- BUG-016: Province field on Property Register AND Edit, and Owner Register AND Edit uses a `<select>` dropdown with the 9 SA provinces (not a free-text `<input>`)
- BUG-017: PropertyOwner model has `[Display]` attributes on all user-facing fields; labels in Owner/Register show "First Name", "Last Name", etc.
- BUG-020: FileMaster Index table includes an Assessment Track column with readable labels
- BUG-021: SeedDataService seeds at least 8 WaterSource values and 7 IrrigationSystem values; `dotnet build` passes
- MISSING-001: ApplicationUser has `IsHdi` bool; migration added and applied; Admin Create and Edit forms include HDI checkbox; controller wires the field in both Create and Edit paths
- `dotnet build` passes with 0 errors after all changes

## Journal

### 2026-05-22T08:00+02:00 — Controller — briefing + context gathering
- Read: Controllers/HomeController.cs, Views/Home/Index.cshtml, Views/FileMaster/Index.cshtml, Views/Property/Register.cshtml, Views/Owner/Register.cshtml, Models/PropertyOwner.cs, Models/ApplicationUser.cs, ViewModels/Admin/CreateUserViewModel.cs, ViewModels/Admin/EditUserViewModel.cs, Controllers/Admin/UsersController.cs, Views/Users/Create.cshtml, Views/Users/Edit.cshtml, Services/SeedDataService.cs (lines 354-490)
- Learned:
  - HomeController has NO DB injection — pure static HTML in Index.cshtml with hardcoded 11 March 2026 date and numbers
  - Province field exists at line 120 in Property/Register.cshtml (`asp-for="Address.Province"` as `<input>`)
  - PropertyOwner model has NO `[Display]` attributes on FirstName, LastName, IdentityDocumentNumber, DateOfBirth, EmailAddress, PhoneNumber
  - FileMaster/Index.cshtml has 6 columns (Reg No, Farm Name, SG Code, Catchment, Status, Property) — no AssessmentTrack
  - SeedWaterSourcesAsync() seeds 6 items: River, Borehole, Dam, Spring, Irrigation Canal, Wetland
  - SeedIrrigationSystemsAsync() seeds 5 items: Drip, Sprinkler, Flood/Furrow, Centre Pivot, Micro-irrigation
  - ApplicationUser has NO IsHdi field; ViewModels and views have no IsHdi
  - PropertyOwner already has IsHDI (capitalized differently); MISSING-001 is for ApplicationUser staff profile
  - Owner/Register.cshtml already has IsHDI checkbox for PropertyOwner (correct)
- Status: DISPATCHING dotnet-master

### 2026-05-22T11:35+02:00 — dotnet-master — implementation of all 6 fixes
- Read: journal (prior entry), CLAUDE.md context, all target files plus FileMaster.cs and LetterIssuance.cs (to verify field names).
- **Important deviation (BUG-009):** the spec's KPI queries referenced `LetterIssuance.ResponseReceivedDate`, which does NOT exist on the model (grep confirmed zero usages repo-wide). The actual field is `ResponseDate` (DateOnly?). I used `ResponseDate` for both the Overdue Tasks and Letters Pending queries — same intent (letters with a due date and no response yet). Also used `DateOnly today = DateOnly.FromDateTime(DateTime.Today)` as a local instead of inlining, since `DueDate` is `DateOnly?` and the comparison needs a `DateOnly`.
- Changes:
  - BUG-009: Controllers/HomeController.cs — injected ApplicationDBContext, added `using Microsoft.EntityFrameworkCore;`, made Index async, 5 ViewBag KPIs. Views/Home/Index.cshtml — dynamic date + all 5 KPI card values; reworded In Process / Overdue / Letters Pending subtitles to match real data (removed fabricated breakdowns).
  - BUG-016: `<select>` with 9 SA provinces in Property/Register.cshtml, Property/Edit.cshtml (asp-for="Address.Province", tabindex 14), Owner/Register.cshtml, Owner/Edit.cshtml (asp-for="Address!.Province", no tabindex).
  - BUG-017: Models/PropertyOwner.cs — added `using System.ComponentModel.DataAnnotations;` and `[Display]` on FirstName, LastName, Title, Gender, IdentityDocumentNumber, DateOfBirth, EmailAddress, PhoneNumber, IsHDI, CustomerTypeId. AddressId left bare per spec.
  - BUG-020: Views/FileMaster/Index.cshtml — `<th>Track</th>` as 6th column (after Status, before Property) + badge cell with switch on AssessmentTrack.
  - BUG-021: Services/SeedDataService.cs — WaterSources now 8 (added Stream, Canal / Furrow, both SURFACE); IrrigationSystems now 7 (added Drag-line, Travelling Irrigator).
  - MISSING-001: Models/ApplicationUser.cs `IsHdi`; CreateUserViewModel + EditUserViewModel `IsHdi`; UsersController Create POST sets it, Edit GET loads it, Edit POST saves it; Create.cshtml HDI checkbox tabindex 7 (InitialPassword bumped to 8); Edit.cshtml HDI checkbox tabindex 6 (IsActive bumped to 7).
  - EF migration `20260522093246_AddIsHdiToApplicationUser` — single `IsHdi bit NOT NULL DEFAULT 0` on AspNetUsers; applied to localhost DB successfully.
- Verification: `dotnet build` → 0 errors (pre-existing NU190x/CS8602 warnings unchanged). `dotnet ef database update` applied cleanly.
- Next agent should know: if any test or report references `ResponseReceivedDate` for KPIs, it must use `ResponseDate`. Out-of-scope items (BUG-019, BUG-022, tests) untouched.
- Status: DONE_WITH_CONCERNS (one spec/model field-name mismatch resolved by substituting the real field `ResponseDate`)

## Retro (on completion)
_To be filled on sprint close._
