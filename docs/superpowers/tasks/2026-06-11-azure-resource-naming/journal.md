# Task: Human-readable Azure resource & deployment-artifact naming

**Start:** 2026-06-11T11:00+02:00
**Branch:** main (docs-only output; controller commits on a docs branch)
**Worktree:** /Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai (main repo, no worktree)
**Plan:** n/a — direct user request
**Acceptance criteria:**
- Inventory of current Azure/AKS resources whose names are UUID-coded or auto-generated, with proposed human-readable names and rename feasibility (renameable in place vs requires recreation).
- A written naming convention covering all deployment-time names we control (Azure resources, ACR repos/tags, Helm release/chart names, k8s namespaces/objects/secrets/PVCs, CI workflow names) so every future deployment uses human-readable names.
- NO changes applied to live Azure resources or cluster — documentation and config-as-code proposals only (deploy approval gate stands).

## Journal

### Entry: devops-platform-strategist — 2026-06-11

**What I inspected (read-only):**
- `az account show` — confirmed subscription PBT-DEV (`f8c1fe57`), tenant Purple-Blue Technologies, user `ematlapeng@majitechglobal.com`. Login active, no NEEDS_CONTEXT issues.
- `az group list` — 4 resource groups: `MajiTech_RG`, `VnV-Project` (main), `MC_VnV-Project_vnv-aks_southafricanorth` (AKS node RG), `NetworkWatcherRG`.
- `az resource list -g VnV-Project` — 4 resources: `vnv-aks` (AKS), `vnvregistry` (ACR Basic), `vnvbackups` (StorageV2 LRS), `dwavvstorage` (StorageV2 LRS).
- `az resource list -g MC_VnV-Project_vnv-aks_southafricanorth` — 9 resources: 2× public IPs (UUID + `kubernetes-{hash}`), load balancer (`kubernetes`), managed identity (`vnv-aks-agentpool`), NSG (`aks-agentpool-25307706-nsg`), VNet (`aks-vnet-25307706`), VMSS (`aks-nodepool1-11537223-vmss`), 2× managed disks (`pvc-{uuid}`).
- `az aks show` — cluster K8s 1.34, 2× Standard_D2s_v3 nodes, node pool name `nodepool1`, DNS prefix `vnv-aks-VnV-Project-f8c1fe`.
- `az acr list` + `az acr repository show-tags` — one repo `dwa-vv`, 23 tags: mix of full 40-char SHAs and 7-char SHAs; no semantic version tags present.
- `kubectl get pv,pvc -A` — 2 PVCs: `dwa-vv-app-blobs` (5Gi, managed-csi) and `dwa-vv-mssql-data` (10Gi, default StorageClass, Retain policy).
- `kubectl get svc -A` — `dwa-vv` LoadBalancer at 20.87.59.203; `ingress-nginx-controller` LoadBalancer `<pending>` (installed but unused).
- `helm list -A` — 4 releases: `dwa-vv` (default, revision 27, pending-upgrade), `cert-manager`, `ingress-nginx`, `aks-managed-overlay-upgrade-data`.
- `kubectl get namespaces` — all app objects in `default`; separate namespaces for `cert-manager`, `ingress-nginx`.
- Public IP details: UUID-named IP is the outbound SNAT IP (20.87.198.167); `kubernetes-ab55fb...` IP is the `dwa-vv` service IP (20.87.59.203). Both tagged with `aks-managed-cluster-name: vnv-aks`.
- Disk tags confirm `kubernetes.io-created-for-pvc-name` annotations — disks are CSI-provisioned, names are PV UIDs.
- `dwa-vv` Helm release is currently in `pending-upgrade` state (a new rollout was in progress at audit time).

**Key findings:**
1. User-controlled resources (`VnV-Project` RG, `vnv-aks`, `vnvregistry`, `vnvbackups`, `dwavvstorage`) are all readable but inconsistent — mixed conventions, no `dev`/`prod` environment qualifier.
2. ALL UUID / hash names are in the Azure-managed MC_ node resource group. None of these can be renamed — they are AKS infrastructure internals. The only lever is: (a) tags, (b) pre-created named PIPs via service annotation, (c) static disk provisioning, (d) named node pool (`nodepool1` → `system`), (e) `--node-resource-group` at next cluster creation.
3. ACR tags are opaque (raw 40-char and 7-char SHAs only). No semantic versioning in place.
4. The ingress-nginx LoadBalancer is `<pending>` — the controller is deployed but no DNS/TLS is wired, so app is exposed directly through the `dwa-vv` service LB instead of going through ingress. This is consistent with the AKS-ROLLOUT.md noting no ingress/TLS on dev.
5. `dwa-vv-mssql-data` PVC uses `Retain` reclaim policy — the disk persists if the PVC is deleted, which is correct for production safety but means a rename/migration requires extra care.

**What I wrote:**
- `/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai/docs/AZURE-RESOURCE-NAMING-AUDIT.md` — full audit table with proposed names, rename feasibility for all resources, 4-section remediation plan ordered by risk (zero-risk tags → low-risk PIP annotation → medium-risk namespace migration → high-risk cluster rebuild), production naming convention table, and the definitive list of names that can never be made human-readable due to Azure architecture constraints.

**Status:** DONE
- All acceptance criteria for the platform-strategist slice met.
- No Azure, Kubernetes, or Helm mutations performed.
- Parallel agent (devops-deployment-architect) owns `docs/DEPLOYMENT-NAMING-CONVENTION.md` — not touched.

---

### Entry: devops-platform-strategist (reconciliation) — 2026-06-11

**What changed:** Controller reconciliation ruling — audit doc proposed names were in suffix
style (`dwa-vv-dev-rg`, `dwa-vv-dev-aks`, etc.) and did not match the CAF-prefix convention
established in `docs/DEPLOYMENT-NAMING-CONVENTION.md` (`rg-dwa-vv-dev`, `aks-dwa-vv-dev`, etc.).
Also used `dwa-vv` as the namespace instead of the env-qualified `dwa-vv-dev`.

**Edits applied to `docs/AZURE-RESOURCE-NAMING-AUDIT.md` (proposed names only — current names unchanged):**
- RG: `dwa-vv-dev-rg` → `rg-dwa-vv-dev`
- AKS cluster: `dwa-vv-dev-aks` → `aks-dwa-vv-dev`
- ACR: `dwavvdevacr` → `acrdwavv` (shared across envs per convention doc)
- Backup storage: `dwavvdevbackups` → `stdwavvbackupdev`
- Node RG: `dwa-vv-dev-nodes-rg` → `rg-dwa-vv-dev-nodes`
- PIP: `dwa-vv-dev-pip` → `pip-dwa-vv-ingress-dev`
- Namespace: `dwa-vv` → `dwa-vv-dev`
- SQL disk: `dwa-vv-dev-mssql-data` → `disk-dwa-vv-mssql-data-dev`
- SQL PV: `dwa-vv-dev-mssql-disk` → `pv-dwa-vv-mssql-data-dev`
- Section 6 production table rebuilt to match convention doc column structure (dev + prod side by side)
- Added cross-reference line near top: "Proposed names follow the authoritative convention in `docs/DEPLOYMENT-NAMING-CONVENTION.md`."
- No other files touched.

**Status:** DONE

### Entry: devops-deployment-architect — 2026-06-11

**What I read:**
- `.github/workflows/deploy-azure.yml` — the active CI workflow (deploys on push to main); uses `HELM_NAMESPACE: default`, full 40-char SHA tags, `vnvregistry.azurecr.io`, hardcoded secret name `acr-pull-secret`.
- `.github/workflows/deploy-aks.yml` — manual-trigger AKS workflow; references `dwa-vv` namespace and relies on GitHub vars for ACR/RG/cluster names.
- `deploy/helm/dwa-vv/Chart.yaml`, `values.yaml`, `values.dev.yaml`, `values.prod.yaml` — Helm chart with release name `dwa-vv`; in-cluster mssql; PVC names derived via `dwa-vv.fullname` helper; app-blobs PVC provisioned dynamically by `managed-csi`.
- All Helm templates: `deployment.yaml`, `service.yaml`, `mssql-deployment.yaml`, `mssql-pvc.yaml`, `app-blob-pvc.yaml`, `serviceaccount.yaml`, `ingress.yaml`, `secret-keyvault-csi.yaml`, `mssql-backup-cronjob.yaml`, `configmap.yaml`, `_helpers.tpl` — confirmed all object names are derived from `dwa-vv.fullname`; service has no annotations block (needed for named IP).
- `deploy/aks/provision.sh` — defaults reference `dwaregistry`, `rg-dwa-vv-aks`, `aks-dwa-vv-demo`, `kv-dwa-vv-aks`, `law-dwa-vv`, `mi-dwa-vv-app`; no `--node-resource-group` flag on `az aks create`.
- `deploy/aks/deploy.sh` — references `dwaregistry` as the default ACR; namespace defaults to `dwa-vv`.
- `deploy/provision-azure.sh` — App Service path; uses `rg-dwa-vv-demo`, `plan-dwa-vv-demo`, `dwa-vv-demo`, `sql-dwa-vv-demo`.
- `docs/AKS-ROLLOUT.md` — runbook uses example names that don't match convention.

**What I learned:**
- The critical naming gaps are: (1) ACR name drift `dwaregistry` vs `vnvregistry` vs target `acrdwavv`; (2) k8s namespace `default` instead of `dwa-vv-dev`; (3) no mechanism in place to name the LoadBalancer public IP or managed disks at provision time; (4) full SHA image tags; (5) node resource group auto-generated as `MC_...`.
- PVC-backed managed disks get UUID names unless you either pre-create the disk and bind a PV, or use a custom StorageClass. This is the hardest thing to fix in place — requires a data migration (backup, delete, restore).
- The LoadBalancer public IP can be controlled with one service annotation plus a pre-created named PIP in the node RG. This is safe to apply on next deployment without recreating anything.
- Helm object names are already internally consistent (derived from release name `dwa-vv`). No template changes needed there except: (a) `imagePullSecrets` name and (b) adding an `annotations` pass-through to the Service template.

**What I wrote:**
- `/Users/edwinmatlapeng/dotnet/dwa_val-ver/dwa_ver_val ai/docs/DEPLOYMENT-NAMING-CONVENTION.md` — authoritative naming convention covering: Azure resource groups, AKS cluster + node resource group, ACR, SQL, Key Vault, managed identity, App Service, storage accounts; ACR image tags; Helm release/chart/namespace; all k8s objects; GitHub Actions workflows. Includes rename feasibility table for live resources, rules checklist, mechanism explanations for auto-named objects (LoadBalancer IPs, managed disks, node RG), and 9 fenced-code proposed diffs for `provision.sh`, `deploy.sh`, `provision-azure.sh`, `deploy-azure.yml`, `deploy-aks.yml`, `deployment.yaml`, `values.dev.yaml`, `service.yaml`, and `AKS-ROLLOUT.md`.

**Status:** DONE
- All acceptance criteria met.
- No live Azure or cluster changes made.
- No workflow files, Helm files, or deploy scripts modified — diffs proposed in doc only.
- Known pre-existing drift items documented in Section 9 of the convention doc.


## Retro (on completion)

Converged: both agents independently identified the same root causes (UUID names all live in the AKS-managed MC_ resource group; controllable only via pre-created PIP + annotation, pre-provisioned disks, and `--node-resource-group` at cluster create) and the same zero-/low-risk remediation order. Drifted: the two agents proposed different naming styles (CAF prefix `rg-dwa-vv-dev` vs suffix `dwa-vv-dev-rg`) and different namespace targets (`dwa-vv-dev` vs `dwa-vv`) because the briefing packet did not pin the style upfront — controller ruled for CAF prefix style and re-dispatched the audit agent to align, which it did cleanly. Failed prompt pattern: omitting the concrete naming pattern from the shared packet when two agents must propose names independently; next time the packet should fix the style (or name one doc as authority) before parallel dispatch. No live Azure changes were made; deploy approval gate held throughout.
