# Auth Claims Contract

**Owners:** security-architect (producer), dotnet-architect (consumers)

## Shape

Every authenticated request carries the following claims:

| Claim type | Value | Source |
|---|---|---|
| `ClaimTypes.NameIdentifier` | ApplicationUser.Id (Guid as string) | Identity default |
| `ClaimTypes.Name` | UserName (= email) | Identity default |
| `ClaimTypes.Email` | Email | Identity default |
| `ClaimTypes.Role` | one of: SystemAdmin, NationalManager, RegionalManager, Validator, Capturer, ReadOnly (multiple allowed; usually one per user) | Identity role store |
| `orgUnitId` | ApplicationUser.OrgUnitId (Guid) or empty string if unscoped | DwsClaimsTransformation |
| `provinceId` | OrganisationalUnit.ProvinceId (Guid) or empty string | DwsClaimsTransformation |
| `wmaId` | OrganisationalUnit.WmaId (Guid) or empty string | DwsClaimsTransformation |
| `catchmentId` | OrganisationalUnit.CatchmentAreaId (Guid) or empty string | DwsClaimsTransformation |
| `displayName` | FirstName + " " + LastName | DwsClaimsTransformation |
| `employeeNumber` | ApplicationUser.EmployeeNumber | DwsClaimsTransformation |

## Fixture

`contracts/fixtures/auth/claims.json` — canonical claims for a Validator user scoped to Limpopo WMA.

## Producer

`Services/Auth/DwsClaimsTransformation.cs` — implements `IClaimsTransformation`, called by ASP.NET Core on every request to augment the Identity-emitted ClaimsPrincipal.

## Consumers

- `Services/Auth/DwsPolicies.cs` — registers authorisation policies; policy handlers read Role + scope claims.
- `Services/Auth/IScopedCaseQuery.cs` — reads scope claims to filter `FileMaster`/`Property` queries.
- Any `[Authorize(Policy = "…")]` controller action in the codebase.

## Invariants

- `orgUnitId` must either be a valid `OrganisationalUnit.OrgUnitId` or the empty string. Never null; never a malformed GUID.
- A user with role `NationalManager` or `SystemAdmin` bypasses scope filtering regardless of `orgUnitId` value.
- A user may hold multiple roles; the highest-privilege role determines scope-bypass eligibility.
- Claims are rebuilt on every request by `DwsClaimsTransformation` — never cached in a long-lived token.

## How to change this contract

1. Update `docs/contracts/auth-claims.md` and `contracts/fixtures/auth/claims.json` in the SAME commit.
2. Run the two-agent review (security-architect + dotnet-architect) on the doc+fixture diff.
3. Update `DwsClaimsTransformation`; verify the producer unit test against the fixture.
4. Update any consumer (policy handler, scope filter); verify their tests against the fixture.
5. Append one-liner to `docs/contracts/CHANGELOG.md`.
