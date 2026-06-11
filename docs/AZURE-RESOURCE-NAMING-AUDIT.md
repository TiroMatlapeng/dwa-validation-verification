# Azure Resource Naming Audit — DWA V&V Project

**Audit date:** 2026-06-11
**Subscription:** PBT-DEV Subscription (`f8c1fe57-1ceb-44df-a684-1271120b1215`)
**Tenant:** Purple-Blue Technologies (`purplebluetechnologies.onmicrosoft.com`)
**Cluster context:** `vnv-aks` (AKS, southafricanorth, Kubernetes 1.34)
**Scope:** read-only inventory — no mutations applied

Proposed names follow the authoritative convention in `docs/DEPLOYMENT-NAMING-CONVENTION.md`.

---

## 1. Executive Summary

The Azure resource inventory shows a mixed picture. The resources you control directly (main
resource group, AKS cluster, ACR, storage accounts, Kubernetes objects, Helm release) are
already using recognisable project-scoped names (`vnv-aks`, `vnvregistry`, `dwa-vv`, etc.).
The UUID / auto-generated names are almost entirely confined to the **MC_ node resource group**,
which Azure manages on your behalf and whose contents cannot be renamed in place for any
resource type. A small number of items (ACR image tags, the ingress-nginx load balancer state)
deserve remediation.

---

## 2. Full Resource Inventory

### 2.1 User-Controlled Resources — VnV-Project Resource Group

| Current name | Resource type | Assessment | Proposed name | Rename feasibility |
|---|---|---|---|---|
| `VnV-Project` | Resource group | Readable but inconsistent capitalisation; hyphenated vs PascalCase convention | `rg-dwa-vv-dev` | **Recreate required** — resource groups cannot be renamed; migration = create new RG, move resources (supported for all types here) |
| `vnv-aks` | AKS managed cluster | Recognisable but project-agnostic (`vnv` alone is ambiguous) | `aks-dwa-vv-dev` | **Recreate required** — AKS clusters cannot be renamed; full cluster recreation with data migration needed |
| `vnvregistry` | Azure Container Registry | No hyphens; not project-scoped | `acrdwavv` (ACR names: alphanumeric only, 5–50 chars; shared across envs) | **Recreate required** — ACR cannot be renamed; create new, re-push all images, update kubeconfig/secrets |
| `vnvbackups` | Storage account | Purpose unclear without context | `stdwavvbackupdev` (storage names: alphanumeric only, 3–24 chars) | **Recreate required** — storage accounts cannot be renamed; create new, copy blobs via `azcopy`, update secrets |
| `dwavvstorage` | Storage account | Purpose unclear (used for app blobs PVC?) | `stdwavvappstorage` (rename purpose-specific when use confirmed) | **Recreate required** — same as above |

**Verdict on user-controlled names:** All five are readable enough to operate with. The changes
above are correct-direction but carry high operational risk on a live dev environment. See
Section 5 for a risk-ordered recommendation.

---

### 2.2 Node Resource Group — MC_VnV-Project_vnv-aks_southafricanorth

This entire resource group is **Azure-managed**. Its name is auto-derived from:
`MC_{resource-group}_{cluster-name}_{location}` and was fixed at cluster creation. You cannot
rename the MC_ group itself, nor its contents, using any Azure rename operation.

| Current name | Resource type | Why it looks like a UUID | Can it be renamed? | Safe remediation path |
|---|---|---|---|---|
| `MC_VnV-Project_vnv-aks_southafricanorth` | Node resource group | Azure-derived name (`MC_` prefix + RG + cluster + region) | **No — fixed at cluster creation** | Recreate cluster with `--node-resource-group rg-dwa-vv-dev-nodes` flag to get a human name next time |
| `aks-nodepool1-11537223-vmss` | Virtual Machine Scale Set | AKS auto-names VMSS: `aks-{poolname}-{hash}-vmss` | **No** — Azure-managed VMSS | Rename node pool from `nodepool1` → `system` or `linux` in new cluster; hash suffix is always present |
| `kubernetes` | Standard Load Balancer | AKS always names its LB `kubernetes` in the MC_ group | **No** — AKS-managed LB | Acceptable as-is; name is predictable and documented by AKS |
| `8b5a01e4-3cf2-423e-84d7-ed4cc36fe9e2` | Public IP (outbound/SNAT) | AKS auto-creates outbound IPs with UUID names | **No** — AKS-managed | Pre-create a named static IP and annotate via `service.beta.kubernetes.io/azure-pip-name`; see Section 4.2 |
| `kubernetes-ab55fb010f938444aa593f492c2e32be` | Public IP (LoadBalancer svc) | AKS derives name from service UID hash | **No — AKS-managed** | Pre-create a named static IP in MC_ group; point `dwa-vv` service at it via `azure-pip-name` annotation; see Section 4.1 |
| `aks-agentpool-25307706-nsg` | Network Security Group | AKS auto-names NSG with `{poolname}-{hash}-nsg` | **No** — AKS-managed | Acceptable — predictable convention; bring-your-own NSG requires BYO VNet setup (out of scope for dev) |
| `aks-vnet-25307706` | Virtual Network | AKS auto-names VNet with `{prefix}-{hash}` | **No** — AKS-managed | For production: pre-create a named VNet and pass via `--vnet-subnet-id` at cluster creation |
| `vnv-aks-agentpool` | User-assigned Managed Identity | AKS creates this for kubelet credentials; name = `{cluster}-agentpool` | **No** — AKS-managed | Acceptable — predictable convention |
| `pvc-f9c0cb3b-df37-4e57-85d2-89e42852b18a` | Managed Disk (SQL data, 10Gi, Retain) | Azure CSI driver names disks after the PV UID | **No — Kubernetes-controlled** | Pre-create a named disk and use static PV provisioning with `volumeName`; see Section 4.3 |
| `pvc-dd24eb3b-3f5b-4243-aa9c-662732bab616` | Managed Disk (app-blobs, 5Gi) | Same as above | **No — Kubernetes-controlled** | Same approach as above |

---

### 2.3 Kubernetes Objects — namespace: default

| Current name | K8s kind | Assessment | Proposed name | Notes |
|---|---|---|---|---|
| `dwa-vv` | Deployment | Good — matches Helm release | `dwa-vv` | Keep |
| `dwa-vv-mssql` | Deployment | Good | `dwa-vv-mssql` | Keep |
| `dwa-vv` | Service (LoadBalancer) | Good | `dwa-vv` | Keep |
| `dwa-vv-mssql` | Service (ClusterIP) | Good | `dwa-vv-mssql` | Keep |
| `dwa-vv-app-blobs` | PVC (5Gi) | Good | `dwa-vv-app-blobs` | Keep |
| `dwa-vv-mssql-data` | PVC (10Gi) | Good | `dwa-vv-mssql-data` | Keep |
| `dwa-vv-env` | ConfigMap | Good | `dwa-vv-env` | Keep |
| `dwa-vv-secrets` | Secret | Good | `dwa-vv-secrets` | Keep |
| `dwa-vv-mssql` | Secret | Good | `dwa-vv-mssql` | Keep |
| `dwa-vv-backup-storage` | Secret | Good | `dwa-vv-backup-storage` | Keep |
| `acr-pull-secret` | Secret | Generic but acceptable | `dwa-vv-acr-pull` | Low-priority rename — requires Helm values update |
| `default` | Namespace | All app workloads in `default` is not ideal | `dwa-vv-dev` | Requires Helm chart namespace change + cluster redeployment; see Section 4.4 |

---

### 2.4 ACR Image Tags — vnvregistry.azurecr.io/dwa-vv

| Current pattern | Issue | Proposed pattern |
|---|---|---|
| Full 40-char git SHA (e.g. `64a25be9512859813c62032bf80b6c291f95740f`) | Unreadable — cannot identify what version without git | `{semver}-{7-char-sha}` e.g. `1.0.0-cec69e5` |
| Short 7-char SHA (e.g. `cb0b764`, `cec69e5`) | Better but still opaque | `{semver}-{7-char-sha}` |

The CI pipeline should tag with both the short SHA (for traceability) and a semantic version
or branch+SHA composite (for readability). Suggested: `1.0.0-dev-cec69e5`. This is a CI
workflow change, not an Azure resource mutation — zero risk.

---

### 2.5 Helm Releases

| Helm release | Namespace | Chart | Assessment |
|---|---|---|---|
| `dwa-vv` | `default` | `dwa-vv-0.1.0` | Name is good. Chart version `0.1.0` should track app versions going forward. |
| `cert-manager` | `cert-manager` | `cert-manager-v1.20.2` | Fine — standard upstream release. |
| `ingress-nginx` | `ingress-nginx` | `ingress-nginx-4.15.1` | Fine — standard upstream release. Note: LoadBalancer IP is `<pending>` — ingress-nginx is installed but not yet used (app exposed directly via `dwa-vv` LoadBalancer service). |
| `aks-managed-overlay-upgrade-data` | `kube-system` | AKS-managed | AKS-controlled — ignore. |

---

## 3. Rename Feasibility Reference

Azure has no rename operation for any resource type in this inventory. The table below states
what each resource type actually supports:

| Resource type | In-place rename? | Authority |
|---|---|---|
| Resource group | No | Azure docs: resource groups cannot be renamed |
| AKS managed cluster | No | Cluster must be recreated; name is immutable |
| Azure Container Registry | No | ACR name is immutable; must delete and recreate |
| Storage account | No | Name is immutable; must create new and migrate data |
| Public IP address (Azure-managed by AKS) | No | AKS owns lifecycle; use annotation-driven pre-created PIPs instead |
| Load balancer (Azure-managed by AKS) | No | AKS-owned |
| VMSS (AKS node pool) | No | Managed by AKS; node pool name can be set at creation |
| Managed disk (CSI-provisioned) | No | Disk name = PV UID; use static provisioning to control name |
| NSG / VNet (AKS-managed) | No | AKS-owned; use BYO VNet to control naming |
| Tags / Display names | Yes | Tags are always mutable |

**The one mechanism that lets you attach a human-readable label without recreation:** Azure
resource **tags**. Every resource in both resource groups should have at minimum:
`project=dwa-vv`, `environment=dev`, `owner=majitech`. Tags can be applied with
`az tag update --resource-id ... --tags ...` and do not affect runtime behaviour.

---

## 4. Actionable Remediation Paths

Items are ordered by risk: zero-risk first, recreate-required last.

### 4.1 Zero-risk: Apply resource tags (do this first)

Tags are mutable on all Azure resources. Apply these to every resource in `VnV-Project` and
the MC_ group:

```
project    = dwa-vv
environment = dev
owner      = majitech
cost-centre = vnv-project
```

Command pattern (read-only shown here — do not run until approved):

```bash
# Example only — requires user go-ahead before execution
az tag update \
  --resource-id $(az resource show -g VnV-Project -n vnv-aks --resource-type Microsoft.ContainerService/managedClusters --query id -o tsv) \
  --operation merge \
  --tags project=dwa-vv environment=dev owner=majitech cost-centre=vnv-project
```

### 4.2 Zero-risk: Fix ACR image tagging in CI pipeline

Change the GitHub Actions workflow to tag images as `{version}-{7-char-sha}` going forward.
All existing tags remain valid — no data loss. This is a CI workflow file change only.
Current tags like `cb0b764` should have the convention applied from the next build.

### 4.3 Low-risk: Pre-create a named static Public IP for the app LoadBalancer

The `dwa-vv` service currently uses `kubernetes-ab55fb010f938444aa593f492c2e32be` as its
public IP (20.87.59.203). To get a human-readable name going forward:

1. Pre-create a Standard SKU static IP in the MC_ resource group:
   ```bash
   # Proposal only — needs user go-ahead
   az network public-ip create \
     --resource-group MC_VnV-Project_vnv-aks_southafricanorth \
     --name pip-dwa-vv-ingress-dev \
     --sku Standard \
     --allocation-method Static \
     --location southafricanorth
   ```
2. Add annotation to the `dwa-vv` Service in Helm values:
   ```yaml
   service:
     annotations:
       service.beta.kubernetes.io/azure-pip-name: "pip-dwa-vv-ingress-dev"
   ```
3. When the service is re-deployed with this annotation, AKS reattaches to the named PIP.
   **Brief interruption**: the old auto-created PIP is released and the new one is attached —
   the external IP address will change unless you keep the same IP by reassigning it.

**Risk level:** Low — brief connectivity gap during service update. Dev environment only,
no SLA to protect. The existing UUID-named PIP is deleted by AKS automatically.

### 4.4 Medium-risk: Move app workloads to a named namespace

Currently all app objects live in `default`. Moving to a `dwa-vv-dev` namespace requires:

1. Update Helm chart: `namespace: dwa-vv-dev`, add `Namespace` template.
2. `kubectl create namespace dwa-vv-dev` (or let Helm create it).
3. Pre-migrate secrets and configmaps to the new namespace.
4. Redeploy via `helm upgrade` — triggers pod recreation, brief downtime on dev.
5. Delete residual objects from `default` once confirmed healthy.

**Risk level:** Medium — pod recreation means app downtime. Acceptable on dev.

### 4.5 Medium-risk: Statically provision named managed disks

For the SQL data disk, replace dynamic PVC provisioning with a static named disk:

1. Pre-create a named disk in the MC_ group (only after approved data backup):
   ```bash
   az disk create \
     --resource-group MC_VnV-Project_vnv-aks_southafricanorth \
     --name disk-dwa-vv-mssql-data-dev \
     --sku StandardSSD_LRS \
     --size-gb 10 \
     --location southafricanorth
   ```
2. Create a static PV manifest pointing to this disk by resource ID:
   ```yaml
   metadata:
     name: pv-dwa-vv-mssql-data-dev
   ```
3. Update the PVC to use `volumeName: pv-dwa-vv-mssql-data-dev` referencing the static PV.
4. Perform a data migration: backup SQL from the old disk, restore to the new.

**Risk level:** Medium-high — involves data migration. Must backup first. Achieves a named
disk but does not change runtime behaviour. Cosmetic benefit does not justify data risk on
production; acceptable on dev if a full SQL backup is taken first.

### 4.6 High-risk / Not recommended for dev: Rename resource group, cluster, ACR

Renaming `VnV-Project` → `rg-dwa-vv-dev`, `vnv-aks` → `aks-dwa-vv-dev`, or
`vnvregistry` → `acrdwavv` requires full recreation:

- Cluster recreation: drain nodes, export all manifests, create new cluster, re-apply
  everything, update kubeconfig in GitHub Actions secrets, update ACR attach.
- ACR recreation: re-push all 23 image tags to the new registry, update imagePullSecret in
  the cluster, update all Helm values.
- Estimated effort: 4–8 hours of ops work. Risk of data loss if not careful with the
  `Retain` reclaim policy disk.

**Recommendation:** Defer until next cluster version upgrade or environment rebuild is
scheduled anyway. At that point, apply the target naming convention from creation.

---

## 5. Recommended Action Plan for Dev Environment

| Priority | Action | Risk | Effort | Who |
|---|---|---|---|---|
| 1 | Apply resource tags to all resources in `VnV-Project` and MC_ group | Zero | 30 min | DevOps |
| 2 | Fix CI pipeline image tag format (`{version}-{7-char-sha}`) | Zero | 30 min | DevOps / Dev |
| 3 | Pre-create `pip-dwa-vv-ingress-dev` and annotate the `dwa-vv` Service | Low (brief IP change) | 1 hr | DevOps |
| 4 | Move app workloads to `dwa-vv-dev` namespace | Medium (pod restart) | 2 hr | DevOps |
| 5 | Static disk provisioning for MSSQL | Medium-high (data migration) | 3 hr | DevOps + DBA |
| 6 | Cluster / RG / ACR recreation with target names | High (full rebuild) | 4–8 hr | Defer to next cluster rebuild |

**Defer items 5 and 6 until production cluster planning is underway.** Items 1–4 can be
done in a single maintenance window without service impact beyond a brief pod restart.

---

## 6. Production Naming Convention (to be applied at next cluster creation)

When the production cluster is provisioned, use these names from day one:

| Resource | Dev / UAT name | Prod name |
|---|---|---|
| Resource group | `rg-dwa-vv-dev` | `rg-dwa-vv-prod` |
| AKS cluster | `aks-dwa-vv-dev` | `aks-dwa-vv-prod` |
| Node resource group | `rg-dwa-vv-dev-nodes` (set via `--node-resource-group`) | `rg-dwa-vv-prod-nodes` |
| ACR | `acrdwavv` (shared across envs; no hyphens; globally unique) | `acrdwavv` |
| Backup storage account | `stdwavvbackupdev` | `stdwavvbackupprod` |
| App LoadBalancer PIP | `pip-dwa-vv-ingress-dev` | `pip-dwa-vv-ingress-prod` |
| K8s namespace | `dwa-vv-dev` | `dwa-vv-prod` |
| Helm release | `dwa-vv` | `dwa-vv` |
| Node pool | `system` (default system pool) | `system` |
| VMSS | Auto-named by AKS as `aks-system-{hash}-vmss` — acceptable with named pool | same |
| SQL data disk | `disk-dwa-vv-mssql-data-dev` (static provisioning) | `disk-dwa-vv-mssql-data-prod` |
| SQL data PV | `pv-dwa-vv-mssql-data-dev` | `pv-dwa-vv-mssql-data-prod` |
| App blobs disk | dynamic provisioning until blob store migrated to Azure Blob Storage | `disk-dwa-vv-app-blobs-prod` |
| Image tags | `{7-char-sha}` + `dev-latest` env tag | `{7-char-sha}` + `prod-latest` env tag |

Note: ACR and storage account names must be globally unique across all of Azure; add a short
random suffix (e.g. 4 alphanumeric chars) if the above names are taken: `acrdwavv7k2m`.

---

## 7. What Cannot Be Fixed (Azure Architecture Constraints)

The following names will always be UUID / hash-derived regardless of what you configure,
because Azure generates them at runtime:

- **VMSS hash suffix** (`aks-system-{hash}-vmss`) — always includes an 8-char pool hash.
  Mitigate by using a meaningful pool name (`system` rather than `nodepool1`) — the prefix
  becomes readable even if the suffix is not.
- **Load balancer frontend names** (`{uuid}`, `{hash}`) — AKS internal LB configuration;
  not user-facing.
- **PV object names** (`pvc-{uuid}`) — always derived from the PV UID unless you use static
  provisioning (see 4.5).
- **Helm release secrets** (`sh.helm.release.v1.dwa-vv.vN`) — Helm internals; not
  user-facing.
- **cert-manager / ingress-nginx internal names** — upstream charts; not configurable.

---

*This document is read-only infrastructure analysis. No Azure or Kubernetes resources were
modified during this audit. All mutations require explicit user approval per the project
deploy approval gate.*
