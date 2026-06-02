# BUG-019 — Appendix D Dam Calculator Inputs on Create

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose Appendix D Method 1/2 calculator inputs on the Create form and persist them, so users can enter inputs during creation and land on Edit ready to calculate.

**Architecture:** Two file changes only. The calculator (`DamVolumeCalculator`, `CalculatorService`) and the ViewModel already have all fields. The gap is the Create view (missing the Appendix D section) and the Create POST (not mapping the 8 input fields or redirecting to Edit). Edit already works end-to-end and is the reference pattern.

**Tech Stack:** ASP.NET Core 10 MVC, Razor Views, C#

---

## File Map

| Action | File | What changes |
|--------|------|--------------|
| Modify | `Views/DamCalculation/Create.cshtml` | Add Appendix D section + update DamCapacity hint text |
| Modify | `Controllers/DamCalculationController.cs` | Create POST: map 8 fields; redirect to Edit |

No model changes, no ViewModel changes, no migrations, no new unit tests (calculator is already tested in `Tests/Services/Calculator/DamVolumeCalculatorTests.cs`).

---

## Task 1: Add Appendix D section to Create.cshtml

**Files:**
- Modify: `Views/DamCalculation/Create.cshtml`

- [ ] **Step 1: Open the file and locate the DamCapacity hint text**

  In `Views/DamCalculation/Create.cshtml` around line 56, find:
  ```html
  <small style="color: var(--dws-text-muted);">Use Appendix D Method 1 (Wall Length) or Method 2 (Area) to calculate before entering.</small>
  ```
  Replace it with:
  ```html
  <small style="color: var(--dws-text-muted);">Enter Appendix D inputs below and save — then click Calculate Capacity on the next screen.</small>
  ```

- [ ] **Step 2: Add the Appendix D form section**

  Insert the following block immediately before the submit button row (the `<div style="margin-top: 20px; display: flex; gap: 8px;">` div):

  ```html
  <div class="form-section-title" style="margin-top: 20px;">Appendix D Volume Calculation</div>

  <div class="form-group">
      <label class="form-label">Calculation Method</label>
      <select id="calcMethod" name="CalculationMethod" class="form-control" style="width:100%;" onchange="toggleMethod()">
          <option value="">-- Select Method --</option>
          <option value="Method1">Method 1 — Wall Length</option>
          <option value="Method2">Method 2 — Area</option>
      </select>
  </div>

  <div id="method1-fields" style="display:none;">
      <div class="form-row">
          <div class="form-group">
              <label class="form-label">Wall Length (m)</label>
              <input type="number" step="0.01" name="WallLength" class="form-control" style="width:100%;" />
          </div>
          <div class="form-group">
              <label class="form-label">Fetch (m)</label>
              <input type="number" step="0.01" name="Fetch" class="form-control" style="width:100%;" />
          </div>
      </div>
      <div class="form-row">
          <div class="form-group">
              <label class="form-label">River Distance R1 (m)</label>
              <input type="number" step="0.01" name="RiverDistance" class="form-control" style="width:100%;" />
          </div>
          <div class="form-group">
              <label class="form-label">Contour Difference C1 (m)</label>
              <input type="number" step="0.01" name="ContourDifference" class="form-control" style="width:100%;" />
          </div>
      </div>
  </div>

  <div id="method2-fields" style="display:none;">
      <div class="form-row">
          <div class="form-group">
              <label class="form-label">Dam Area (ha)</label>
              <input type="number" step="0.01" name="DamArea" class="form-control" style="width:100%;" />
          </div>
          <div class="form-group">
              <label class="form-label">Dam Depth (m)</label>
              <input type="number" step="0.01" name="DamDepth" class="form-control" style="width:100%;" />
          </div>
      </div>
  </div>

  <div class="form-group" id="shape-factor-group" style="display:none;">
      <label class="form-label">Shape Factor</label>
      <select name="ShapeFactor" class="form-control" style="width:280px;">
          <option value="">-- Select Shape --</option>
          <option value="0.33">0.33 — Ravine (triangle)</option>
          <option value="0.40">0.40 — Square with bends</option>
          <option value="0.50">0.50 — Circular</option>
      </select>
  </div>
  ```

- [ ] **Step 3: Add the toggleMethod() script**

  Add a `@section Scripts` block at the bottom of the file (after the closing `</div>` of the card):

  ```html
  @section Scripts {
      <script>
          function toggleMethod() {
              var v = document.getElementById('calcMethod').value;
              document.getElementById('method1-fields').style.display = v === 'Method1' ? '' : 'none';
              document.getElementById('method2-fields').style.display = v === 'Method2' ? '' : 'none';
              document.getElementById('shape-factor-group').style.display = v ? '' : 'none';
          }
          toggleMethod();
      </script>
  }
  ```

- [ ] **Step 4: Build to verify no syntax errors**

  ```bash
  cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet build
  ```

  Expected: `Build succeeded.` with 0 errors (pre-existing warnings are fine).

---

## Task 2: Update Create POST — map fields and redirect to Edit

**Files:**
- Modify: `Controllers/DamCalculationController.cs` — `Create` POST action (~lines 73–111)

- [ ] **Step 1: Add the 8 calculator fields to the entity initializer**

  In the `Create` POST action, locate the `new DamCalculation { ... }` initializer. It currently ends with:
  ```csharp
  DamCapacity = vm.DamCapacity,
  DamCalculationStatus = vm.DamCalculationStatus
  ```
  Extend it to:
  ```csharp
  DamCapacity = vm.DamCapacity,
  DamCalculationStatus = vm.DamCalculationStatus,
  CalculationMethod = vm.CalculationMethod,
  WallLength = vm.WallLength,
  Fetch = vm.Fetch,
  RiverDistance = vm.RiverDistance,
  ContourDifference = vm.ContourDifference,
  DamArea = vm.DamArea,
  DamDepth = vm.DamDepth,
  ShapeFactor = vm.ShapeFactor,
  ```

- [ ] **Step 2: Change the redirect to point at Edit**

  Find:
  ```csharp
  TempData["Success"] = "Dam Calculation record added successfully.";
  return RedirectToAction(nameof(Index), new { propertyId = vm.PropertyId });
  ```
  Replace with:
  ```csharp
  TempData["Success"] = "Dam Calculation record added. Enter Appendix D inputs and click Calculate Capacity.";
  return RedirectToAction(nameof(Edit), new { id = entity.DamCalculationId });
  ```

- [ ] **Step 3: Build to verify**

  ```bash
  cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet build
  ```

  Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 4: Run existing calculator tests to confirm nothing regressed**

  ```bash
  cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet test --filter "DamVolumeCalculator" --verbosity normal
  ```

  Expected: 5 tests pass (`Method1_ComputesCorrectCapacity`, `Method1_RavineShapeFactor_UsesPointThreeThree`, `Method1_ThrowsWhenContourDifferenceIsZero`, `Method2_ComputesCorrectCapacity`, `Method2_ZeroDepth_ReturnsZero`).

- [ ] **Step 5: Commit**

  ```bash
  cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && git add Views/DamCalculation/Create.cshtml Controllers/DamCalculationController.cs && git commit -m "$(cat <<'EOF'
  fix(bug-019): add Appendix D inputs to DamCalculation Create form

  - Create.cshtml: method selector, M1/M2 conditional inputs, shape factor, toggleMethod() JS
  - Create POST: map 8 calculator fields to entity; redirect to Edit after save
  - Hint text updated to guide user to Calculate Capacity on next screen

  Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
  EOF
  )"
  ```

---

## Task 3: Manual smoke test

- [ ] **Step 1: Start the app**

  SQL Server must be running in Docker before starting.

  ```bash
  cd "/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai" && dotnet run
  ```

- [ ] **Step 2: Navigate to a property's dam calculations and click Add**

  Go to `DamCalculation/Create?propertyId=<any-valid-guid>`. Verify the Appendix D section is visible below the Capacity & Status row.

- [ ] **Step 3: Test Method 1 toggle**

  Select **Method 1 — Wall Length**. Verify the four Method 1 inputs appear and Method 2 inputs remain hidden. Verify the Shape Factor dropdown appears.

- [ ] **Step 4: Test Method 2 toggle**

  Switch to **Method 2 — Area**. Verify Method 1 inputs hide and the two Method 2 inputs appear. Shape Factor remains visible.

- [ ] **Step 5: Save and verify redirect**

  Fill in: Dam Number, River, dates, Method 1 inputs (e.g. WallLength=200, Fetch=50, RiverDistance=100, ContourDifference=10), Shape Factor 0.40, DamCapacity=0, Status=IN_PROGRESS. Click **Save Record**.

  Expected: redirected to **Edit** page for the newly created record (not Index). TempData banner reads "Dam Calculation record added. Enter Appendix D inputs and click Calculate Capacity."

- [ ] **Step 6: Verify Calculate Capacity works**

  On the Edit page, confirm all inputs are pre-filled from the Create form. Click **Calculate Capacity**.

  Expected: TempData success banner shows `Dam capacity calculated: 10,000 m³` (for the values above: 200×50×5×0.4/2 = 10,000).
