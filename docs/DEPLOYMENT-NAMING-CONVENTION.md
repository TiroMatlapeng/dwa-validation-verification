# DWA V&V Deployment Naming Convention

> **Status:** Authoritative reference — proposed names to be applied in a dedicated change.
> **Last updated:** 2026-06-11
> **Scope:** All Azure resources, AKS/Kubernetes objects, ACR artefacts, Helm artefacts, and CI/CD names for the DWA V&V System.
> **Out of scope:** Application source code, database schema, business-logic identifiers.

---

## 1. Why This Document Exists

The live dev/UAT stack (as of 2026-06-11) has several auto-generated or inconsistent names:

- The LoadBalancer public IP is an Azure-assigned ephemeral address with no stable name (`20.87.59.203`).
- PVC-backed managed disks get names like `kubernetes-dynamic-pvc-<uuid>` assigned by AKS at provision time.
- `deploy.sh` and the runbook reference `dwaregistry` as the ACR name while the actual live registry is `vnvregistry` — a naming drift that silently breaks manual runbook steps.
- The GitHub Actions workflow `deploy-azure.yml` is named `Build and deploy to AKS (VnV-Project)` (confusingly references AKS but deploys to App Service).
- There is no declared standard, so each provisioning script invents its own names.

This document defines the one authoritative pattern all future deployments must follow and provides the exact diffs needed to bring existing files into compliance.

---

## 2. Core Naming Pattern

```
<project>-<component>[-<env>]
```

| Segment | Values | Notes |
|---------|--------|-------|
| `<project>` | `dwa-vv` | Always lowercase-hyphenated. Short but descriptive — "DWA V&V System". Never `dwaregistry`, `vnv*`, or `dwa_ver_val`. |
| `<component>` | `web`, `mssql`, `letter-blobs`, `portal-blobs`, `backup`, `identity`, `gateway`, etc. | Describes the function, not the technology. |
| `<env>` | `dev`, `uat`, `prod` | Omit when the resource is shared across environments or when the environment is implicit (e.g., ACR is shared). |

Azure resource-type prefixes follow [Microsoft's recommended abbreviations (CAF)](https://learn.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations):

| Type | Prefix |
|------|--------|
| Resource group | `rg-` |
| AKS cluster | `aks-` |
| Azure Container Registry | `acr-` |
| Azure SQL logical server | `sql-` |
| Key Vault | `kv-` |
| App Service plan | `plan-` |
| App Service (web app) | `app-` |
| Managed identity | `id-` |
| Log Analytics workspace | `log-` |
| Service principal | `sp-` |
| Static public IP | `pip-` |
| Storage account | `st` (no hyphen — storage accounts forbid hyphens, max 24 chars) |

Kubernetes objects do not get a type prefix — the Kind field carries that information.

---

## 3. Authoritative Name Table

### 3.1 Azure Resources

| Resource type | Dev / UAT name | Prod name | Notes |
|--------------|---------------|-----------|-------|
| Resource group | `rg-dwa-vv-dev` | `rg-dwa-vv-prod` | One RG per environment. |
| AKS cluster | `aks-dwa-vv-dev` | `aks-dwa-vv-prod` | |
| AKS node resource group | `rg-dwa-vv-dev-nodes` | `rg-dwa-vv-prod-nodes` | Pass `--node-resource-group rg-dwa-vv-dev-nodes` at `az aks create`. If omitted, AKS generates `MC_<rg>_<cluster>_<region>` — unreadable. |
| Azure Container Registry | `acrdwavv` | `acrdwavv` | Shared across envs. No hyphens allowed in ACR names; 5–50 chars, globally unique. Login server: `acrdwavv.azurecr.io`. |
| Azure SQL logical server | `sql-dwa-vv-dev` | `sql-dwa-vv-prod` | Globally unique on `.database.windows.net`. |
| Azure SQL database | `dwa-vv` | `dwa-vv` | Logical database name. Application connection string uses this. |
| Key Vault | `kv-dwa-vv-dev` | `kv-dwa-vv-prod` | 3–24 chars, globally unique. |
| Managed identity (app) | `id-dwa-vv-app-dev` | `id-dwa-vv-app-prod` | The workload identity bound to the k8s service account. |
| Federated credential | `fed-dwa-vv-aks-dev` | `fed-dwa-vv-aks-prod` | On the managed identity. |
| Log Analytics workspace | `log-dwa-vv-dev` | `log-dwa-vv-prod` | |
| App Service plan (App Service path) | `plan-dwa-vv-demo` | `plan-dwa-vv-prod` | Only used when deploying to App Service (not AKS). |
| App Service web app | `app-dwa-vv-demo` | `app-dwa-vv-prod` | Globally unique on `.azurewebsites.net`. |
| Static public IP (ingress) | `pip-dwa-vv-ingress-dev` | `pip-dwa-vv-ingress-prod` | Must be created in the **node resource group** (see Section 5.1). |
| Storage account (backups) | `stdwavvbackupdev` | `stdwavvbackupprod` | No hyphens, globally unique, max 24 chars. Blob container: `mssql-backups`. |
| Service principal (CI) | `sp-dwa-vv-github` | `sp-dwa-vv-github` | Shared. Used in `AZURE_CREDENTIALS` GitHub secret. |

### 3.2 ACR Image Repository and Tags

| Item | Convention | Example |
|------|-----------|---------|
| Repository name | `dwa-vv` | `acrdwavv.azurecr.io/dwa-vv` |
| Release tag | `<short-git-sha>` (7 chars) | `acrdwavv.azurecr.io/dwa-vv:a1b2c3d` |
| Environment-pinned tag | `<env>-latest` | `acrdwavv.azurecr.io/dwa-vv:dev-latest` |
| Never use | `latest` alone | Ambiguous, un-rollbackable. |

The current live workflow uses the full 40-char `${{ github.sha }}` as the image tag. Switch to `${{ github.sha | slice 0 7 }}` (short SHA) for readability. The full SHA should be stored as an OCI label annotation, not the tag itself.

### 3.3 Helm

| Item | Value | Notes |
|------|-------|-------|
| Helm release name | `dwa-vv` | Do not append env — the namespace provides environment separation. |
| Helm chart name (`Chart.yaml`) | `dwa-vv` | Already correct. |
| Kubernetes namespace | `dwa-vv-dev` | `default` is forbidden — it bypasses namespace-scoped RBAC. Current live value is `default`; this must be migrated (see Section 5.2). |

> **Current deviation:** The active `deploy-azure.yml` deploys into namespace `default`. This must change to `dwa-vv-dev` when the cluster is next re-provisioned.

### 3.4 Kubernetes Object Names

All names are derived by the Helm `dwa-vv.fullname` helper, which resolves to `<release-name>` when the release name already contains the chart name. With release `dwa-vv` in namespace `dwa-vv-dev`, the resolved names are:

| Kind | Name | Notes |
|------|------|-------|
| Deployment (app) | `dwa-vv` | |
| Deployment (SQL) | `dwa-vv-mssql` | |
| Service (app) | `dwa-vv` | |
| Service (SQL, ClusterIP) | `dwa-vv-mssql` | |
| ServiceAccount | `dwa-vv` | |
| ConfigMap | `dwa-vv-env` | |
| Secret (app config) | `dwa-vv-secrets` | |
| Secret (SQL SA password) | `dwa-vv-mssql` | |
| Secret (backup storage) | `dwa-vv-backup-storage` | |
| Secret (ACR pull) | `acr-pull-secret` | Note: this name is hardcoded in the workflow; align with convention (see diff in Section 6.3). |
| PVC (SQL data) | `dwa-vv-mssql-data` | |
| PVC (app blobs) | `dwa-vv-app-blobs` | |
| CronJob (SQL backup) | `dwa-vv-mssql-backup` | |
| HPA | `dwa-vv` | |
| PodDisruptionBudget (app) | `dwa-vv` | |
| PodDisruptionBudget (SQL) | `dwa-vv-mssql` | |
| Ingress | `dwa-vv` | |
| SecretProviderClass | `dwa-vv-kv-secrets` | |
| TLS secret (dev) | `dwa-vv-tls` | |
| TLS secret (prod) | `dwa-vv-prod-tls` | |

### 3.5 GitHub Actions

| Item | Current name | Target name |
|------|-------------|-------------|
| Workflow (`deploy-azure.yml`) | `Build and deploy to AKS (VnV-Project)` | `Deploy: App Service (dev)` |
| Workflow (`deploy-aks.yml`) | `Deploy to AKS` | `Deploy: AKS` |
| Job (build gate) | `build-push-deploy` | `build-push-deploy` (keep — it is functional) |
| Job (build check) | `Build check` | `build-check` |
| Job (deploy) | `Deploy to AKS (${{ inputs.environment }})` | `deploy` (the environment matrix provides context) |
| Artifact (diagnostics) | `aks-diag-${{ inputs.environment }}-${{ github.run_id }}` | Keep — this is already human-readable. |

---

## 4. Rules for Every Future Deployment (Checklist)

Before provisioning any new Azure resource or Kubernetes object, verify each item:

- [ ] **Name matches the pattern** `<project>-<component>[-<env>]` with the correct CAF prefix.
- [ ] **ACR name**: use `acrdwavv` (no hyphens). Update `ACR_NAME` GitHub variable and `deploy.sh` default to `acrdwavv`. Remove all references to `dwaregistry` (the incorrect name that appears in `deploy.sh` comments and the runbook).
- [ ] **Namespace is not `default`**: deploy into `dwa-vv-dev` (dev/UAT) or `dwa-vv-prod` (prod). Update `HELM_NAMESPACE` in `deploy-azure.yml` and the `NAMESPACE` default in `deploy.sh`.
- [ ] **Node resource group is explicitly named**: pass `--node-resource-group rg-dwa-vv-dev-nodes` to `az aks create`. Without this, AKS auto-generates `MC_rg-dwa-vv-dev_aks-dwa-vv-dev_southafricanorth` — a string that embeds region, resource group, and cluster name and becomes hard to reference in firewall/IP rules.
- [ ] **Static public IP is pre-created and annotated** (see Section 5.1).
- [ ] **Managed disk PVC names are explicit** (see Section 5.2).
- [ ] **Image tag is the 7-char short SHA**, not `latest` and not the 40-char full SHA.
- [ ] **No new resource uses a UUID as any part of its name** (UUIDs may appear as the resource's Azure `id` property but never in the name you assign).

---

## 5. Controlling Auto-Generated Names on AKS-Provisioned Azure Objects

AKS provisions certain Azure objects (public IPs, managed disks) automatically in response to Kubernetes API objects. Unless you take deliberate steps, those objects receive UUID-based names. This section explains the exact mechanism to control each one.

### 5.1 LoadBalancer Public IP

When a `Service` with `type: LoadBalancer` is created, AKS allocates a public IP in the **node resource group** with a name like `kubernetes-abc123def456` (auto-generated).

**To use a named static IP:**

1. Create the IP in the node resource group **before** deploying the Service:
   ```bash
   az network public-ip create \
     --resource-group rg-dwa-vv-dev-nodes \
     --name pip-dwa-vv-ingress-dev \
     --sku Standard \
     --allocation-method Static \
     --location southafricanorth
   ```

2. Annotate the Kubernetes Service so AKS binds to it instead of creating a new one:
   ```yaml
   # In deploy/helm/dwa-vv/templates/service.yaml
   metadata:
     annotations:
       service.beta.kubernetes.io/azure-pip-name: "pip-dwa-vv-ingress-dev"
       service.beta.kubernetes.io/azure-load-balancer-resource-group: "rg-dwa-vv-dev-nodes"
   ```

   The annotation value must match the `--name` used in step 1. The resource group must be the **node** resource group (where AKS creates its infrastructure), not the main `rg-dwa-vv-dev`.

3. The IP address is now stable across helm upgrades and cluster re-deployments. Update DNS to point at this IP rather than the raw address.

> **Note:** For the current dev cluster the annotation can be applied on the next planned deployment — there is no need to tear down and recreate anything. The service will re-associate with the named IP on the next reconcile if the IP already exists in the node RG.

### 5.2 PVC-Backed Managed Disks

When a PVC backed by `managed-csi` (Azure disk) is dynamically provisioned, AKS creates a managed disk in the node resource group with a name like `kubernetes-dynamic-pvc-<uuid>`.

**There are two approaches to get a human-readable disk name:**

**Option A — Pre-provisioned disk with a PersistentVolume (recommended for stateful data):**

```bash
# Create the disk with an explicit name
az disk create \
  --resource-group rg-dwa-vv-dev-nodes \
  --name disk-dwa-vv-mssql-data-dev \
  --size-gb 10 \
  --sku Premium_LRS \
  --location southafricanorth

DISK_ID=$(az disk show \
  --resource-group rg-dwa-vv-dev-nodes \
  --name disk-dwa-vv-mssql-data-dev \
  --query id -o tsv)
```

Then create a PersistentVolume that references the disk, and a PVC that references the PV:

```yaml
apiVersion: v1
kind: PersistentVolume
metadata:
  name: pv-dwa-vv-mssql-data-dev
spec:
  capacity:
    storage: 10Gi
  accessModes:
    - ReadWriteOnce
  persistentVolumeReclaimPolicy: Retain
  storageClassName: managed-csi
  csi:
    driver: disk.csi.azure.com
    readOnly: false
    volumeHandle: "<DISK_ID>"   # the full Azure resource ID from above
    volumeAttributes:
      fsType: ext4
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: dwa-vv-mssql-data
  namespace: dwa-vv-dev
spec:
  storageClassName: managed-csi
  volumeName: pv-dwa-vv-mssql-data-dev   # binds to the PV above
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 10Gi
```

**Option B — StorageClass with `diskName` parameter (simpler, less portable):**

Create a custom StorageClass that passes a `diskName` parameter to the CSI driver:

```yaml
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: managed-csi-named
provisioner: disk.csi.azure.com
reclaimPolicy: Retain
volumeBindingMode: WaitForFirstConsumer
parameters:
  skuName: Premium_LRS
  resourceGroup: rg-dwa-vv-dev-nodes
  # The disk will be named with this prefix + PVC UID suffix.
  # Full control requires Option A.
```

> **Recommendation for DWA V&V:** Use Option A for `dwa-vv-mssql-data` (the SQL data disk) since it holds PII and must survive cluster re-provisioning. Use dynamic provisioning (current approach) for `dwa-vv-app-blobs` until the blob store is migrated to Azure Blob Storage (Task 10.x), at which point the PVC disappears entirely.

### 5.3 AKS Node Resource Group Name

The only way to control the node resource group name is at **cluster creation time** via the `--node-resource-group` flag:

```bash
az aks create \
  ...
  --node-resource-group rg-dwa-vv-dev-nodes \
  ...
```

This cannot be changed after cluster creation. The current live cluster's node RG name is auto-generated (`MC_...`). When the cluster is next recreated, pass this flag. The runbook (`deploy/aks/provision.sh`) diff in Section 6.1 includes this flag.

---

## 6. Proposed Diffs

These diffs bring existing files into compliance with this convention. They must be reviewed and applied via a dedicated PR — **do not apply during the authoring of this document**.

### 6.1 `deploy/aks/provision.sh`

Changes:
- Rename all configurable defaults to match the naming table.
- Replace `dwaregistry` with `acrdwavv`.
- Add `--node-resource-group` to `az aks create`.
- Rename managed identity from `mi-dwa-vv-app` to `id-dwa-vv-app-dev`.

```diff
-: "${AZ_RG:=rg-dwa-vv-aks}"
-: "${AZ_ACR:=dwaregistry}"                     # must be globally unique
-: "${AKS_CLUSTER:=aks-dwa-vv-demo}"
-: "${AZ_SQL_SERVER:=sql-dwa-vv-aks}"
-: "${AZ_KV:=kv-dwa-vv-aks}"
-: "${AZ_LAW:=law-dwa-vv}"                      # Log Analytics workspace
-: "${K8S_NAMESPACE:=dwa-vv}"
+: "${AZ_RG:=rg-dwa-vv-dev}"
+: "${AZ_ACR:=acrdwavv}"                        # globally unique; no hyphens
+: "${AKS_CLUSTER:=aks-dwa-vv-dev}"
+: "${AZ_AKS_NODE_RG:=rg-dwa-vv-dev-nodes}"    # explicit node RG name
+: "${AZ_SQL_SERVER:=sql-dwa-vv-dev}"
+: "${AZ_KV:=kv-dwa-vv-dev}"
+: "${AZ_LAW:=log-dwa-vv-dev}"
+: "${K8S_NAMESPACE:=dwa-vv-dev}"
```

```diff
 az aks create \
   -g "$AZ_RG" \
   -n "$AKS_CLUSTER" \
   -l "$AZ_LOCATION" \
+  --node-resource-group "$AZ_AKS_NODE_RG" \
   --node-count "$AKS_NODE_COUNT" \
```

```diff
-APP_MI_NAME="mi-dwa-vv-app"
+APP_MI_NAME="id-dwa-vv-app-dev"
```

### 6.2 `deploy/provision-azure.sh` (App Service path)

Changes:
- Rename `AZ_RG`, `AZ_PLAN`, `AZ_APP`, `AZ_SQL_SERVER` to match the naming table.

```diff
-: "${AZ_RG:=rg-dwa-vv-demo}"
-: "${AZ_PLAN:=plan-dwa-vv-demo}"
-: "${AZ_APP:=dwa-vv-demo}"
-: "${AZ_SQL_SERVER:=sql-dwa-vv-demo}"
+: "${AZ_RG:=rg-dwa-vv-dev}"
+: "${AZ_PLAN:=plan-dwa-vv-dev}"
+: "${AZ_APP:=app-dwa-vv-dev}"
+: "${AZ_SQL_SERVER:=sql-dwa-vv-dev}"
```

### 6.3 `.github/workflows/deploy-azure.yml`

Changes:
- Rename the workflow to `Deploy: App Service (dev)`.
- Change `HELM_NAMESPACE` from `default` to `dwa-vv-dev`.
- Change `ACR_LOGIN_SERVER` to reference `acrdwavv`.
- Rename the ACR pull secret from `acr-pull-secret` to `dwa-vv-acr-pull` so it follows the project prefix convention.

```diff
-name: Build and deploy to AKS (VnV-Project)
+name: Deploy: App Service (dev)
```

```diff
 env:
-  ACR_LOGIN_SERVER: vnvregistry.azurecr.io
+  ACR_LOGIN_SERVER: acrdwavv.azurecr.io
   IMAGE_NAME: dwa-vv
   HELM_RELEASE: dwa-vv
-  HELM_NAMESPACE: default
+  HELM_NAMESPACE: dwa-vv-dev
```

```diff
       - name: Create / refresh ACR pull secret
         run: |
-          kubectl create secret docker-registry acr-pull-secret \
+          kubectl create secret docker-registry dwa-vv-acr-pull \
             --docker-server=${{ env.ACR_LOGIN_SERVER }} \
             --docker-username=${{ secrets.ACR_USERNAME }} \
             --docker-password=${{ secrets.ACR_PASSWORD }} \
             --namespace ${{ env.HELM_NAMESPACE }} \
             --dry-run=client -o yaml | kubectl apply -f -
```

> **Note:** If the ACR pull secret name changes, `deploy/helm/dwa-vv/templates/deployment.yaml` must also change (`imagePullSecrets[0].name`). The diff for that is in Section 6.5.

Image tag: switch from full SHA to short SHA.

```diff
       - name: Build and push image
         uses: docker/build-push-action@v5
         with:
           context: .
           push: true
-          tags: ${{ env.ACR_LOGIN_SERVER }}/${{ env.IMAGE_NAME }}:${{ github.sha }}
+          tags: |
+            ${{ env.ACR_LOGIN_SERVER }}/${{ env.IMAGE_NAME }}:${{ github.sha[:7] }}
+            ${{ env.ACR_LOGIN_SERVER }}/${{ env.IMAGE_NAME }}:dev-latest
```

```diff
       - name: Helm upgrade / install
         run: |
           helm upgrade --install ${{ env.HELM_RELEASE }} deploy/helm/dwa-vv \
             --namespace ${{ env.HELM_NAMESPACE }} \
             -f deploy/helm/dwa-vv/values.dev.yaml \
-            --set image.tag=${{ github.sha }} \
+            --set image.tag=${{ github.sha[:7] }} \
             --wait \
             --timeout 8m
```

### 6.4 `.github/workflows/deploy-aks.yml`

```diff
-name: Deploy to AKS
+name: Deploy: AKS
```

ACR name reference (in `Run deploy.sh` step env):

```diff
       - name: Run deploy.sh
         id: deploy
         env:
-          ACR_NAME:    ${{ vars.ACR_NAME }}
+          ACR_NAME:    acrdwavv          # or keep as ${{ vars.ACR_NAME }} and update the variable
```

Also update the `NAMESPACE` env var default for the smoke-test steps:

```diff
       - name: Capture URL
         id: smoke
         env:
-          NAMESPACE: dwa-vv
+          NAMESPACE: dwa-vv-dev          # or dwa-vv-${{ inputs.environment }}
           RELEASE_NAME: dwa-vv
```

### 6.5 `deploy/helm/dwa-vv/templates/deployment.yaml`

If the ACR pull secret is renamed (Section 6.3):

```diff
       imagePullSecrets:
-        - name: acr-pull-secret
+        - name: dwa-vv-acr-pull
```

### 6.6 `deploy/helm/dwa-vv/values.dev.yaml` — service annotation for named IP

Add the static IP annotation to the service section so the LoadBalancer IP gets a human-readable Azure name. The annotation value must match the `pip-` name you create in Azure before deploying.

```diff
 service:
   type: LoadBalancer
   port: 80
   targetPort: 8080
+  annotations:
+    service.beta.kubernetes.io/azure-pip-name: "pip-dwa-vv-ingress-dev"
+    service.beta.kubernetes.io/azure-load-balancer-resource-group: "rg-dwa-vv-dev-nodes"
```

### 6.7 `deploy/helm/dwa-vv/templates/service.yaml`

Wire through the `service.annotations` values field so the annotations from `values.dev.yaml` are rendered:

```diff
 apiVersion: v1
 kind: Service
 metadata:
   name: {{ include "dwa-vv.fullname" . }}
   namespace: {{ .Release.Namespace }}
   labels:
     {{- include "dwa-vv.labels" . | nindent 4 }}
+  {{- with .Values.service.annotations }}
+  annotations:
+    {{- toYaml . | nindent 4 }}
+  {{- end }}
 spec:
```

### 6.8 `deploy/aks/deploy.sh` — fix the `dwaregistry` drift

The script's usage comment and `AZ_ACR` default reference `dwaregistry` which is incorrect:

```diff
-# export ACR_NAME=dwaregistry
-# export RG=rg-dwa-vv-aks
-# export AKS_CLUSTER=aks-dwa-vv-demo
+# export ACR_NAME=acrdwavv
+# export RG=rg-dwa-vv-dev
+# export AKS_CLUSTER=aks-dwa-vv-dev
```

```diff
-: "${ACR_NAME:?ERROR: ACR_NAME is required (e.g. dwaregistry)}"
+: "${ACR_NAME:?ERROR: ACR_NAME is required (e.g. acrdwavv)}"
```

### 6.9 `docs/AKS-ROLLOUT.md` — update example names in the runbook

The runbook table and command examples use a mix of `dwaregistry`, `rg-dwa-vv-aks`, `aks-dwa-vv-demo`, and `kv-dwa-vv-aks`. Replace all with the names from this document:

```diff
-| `ACR_NAME` | Variable | The ACR name you chose (e.g. `dwaregistry`) |
-| `AZURE_RG` | Variable | Resource group name (e.g. `rg-dwa-vv-aks`) |
-| `AKS_CLUSTER_NAME` | Variable | AKS cluster name (e.g. `aks-dwa-vv-demo`) |
-| `KEY_VAULT_NAME` | Variable | Key Vault name (e.g. `kv-dwa-vv-aks`) |
+| `ACR_NAME` | Variable | `acrdwavv` |
+| `AZURE_RG` | Variable | `rg-dwa-vv-dev` |
+| `AKS_CLUSTER_NAME` | Variable | `aks-dwa-vv-dev` |
+| `KEY_VAULT_NAME` | Variable | `kv-dwa-vv-dev` |
```

---

## 7. Rename Feasibility for Existing Live Resources

The table below covers only the live dev/UAT stack named in the task journal. The parallel audit (`docs/AZURE-RESOURCE-NAMING-AUDIT.md`) inventories actual UUIDs and Azure resource IDs.

| Resource | Current name | Target name | Rename in place? | Notes |
|----------|-------------|-------------|-----------------|-------|
| ACR | `vnvregistry` | `acrdwavv` | No — recreate | ACR names cannot be renamed. New ACR must be created, images re-tagged and pushed, then old ACR deleted. Login server URL changes: update all references. |
| AKS cluster | (unknown exact name) | `aks-dwa-vv-dev` | No — recreate | AKS cluster names cannot be renamed in-place. Recreate on next planned maintenance window. |
| Node resource group | `MC_...` (auto-generated) | `rg-dwa-vv-dev-nodes` | No — set at cluster creation | Pass `--node-resource-group` on next cluster creation. |
| Kubernetes namespace | `default` | `dwa-vv-dev` | No — migrate | Create the new namespace, re-deploy via Helm with updated namespace, delete resources from `default`. Do in a single planned maintenance window. |
| LoadBalancer public IP | (Azure-auto ephemeral) | `pip-dwa-vv-ingress-dev` | Yes — pre-create, then annotate Service | Create named PIP in node RG, add annotation, apply on next deploy. IP address may change unless you request the same address when creating. |
| SQL data managed disk | `kubernetes-dynamic-pvc-<uuid>` | `disk-dwa-vv-mssql-data-dev` | No — requires data migration | Take a backup, delete PVC, create named disk + PV + PVC, restore. Plan a maintenance window. |
| App blobs managed disk | `kubernetes-dynamic-pvc-<uuid>` | `disk-dwa-vv-app-blobs-dev` | No — requires data migration | Same process as SQL disk. Consider migrating to Azure Blob Storage (Task 10.x) at the same time. |
| Helm release | `dwa-vv` | `dwa-vv` | Already correct | No change needed. |
| K8s secrets | `dwa-vv-secrets`, `dwa-vv-mssql`, `dwa-vv-backup-storage` | (same) | Already correct | The Helm template generates these; they match the convention. |
| ACR pull secret | `acr-pull-secret` | `dwa-vv-acr-pull` | Yes — recreate secret | Delete old, apply diff from Section 6.3 + 6.5 together. |

---

## 8. GitHub Actions Variables to Update

When the Azure resources are renamed, update these GitHub Actions repository/environment variables:

| Variable | Old value | New value |
|----------|-----------|-----------|
| `ACR_NAME` | `vnvregistry` (or `dwaregistry`) | `acrdwavv` |
| `AZURE_RG` | (current value) | `rg-dwa-vv-dev` |
| `AKS_CLUSTER_NAME` | (current value) | `aks-dwa-vv-dev` |
| `KEY_VAULT_NAME` | (current value) | `kv-dwa-vv-dev` |

---

## 9. Reference: Known Drift from This Convention (Pre-Existing)

Documented here so future agents can identify these as known deviations, not undiscovered problems:

1. **`dwaregistry` vs `vnvregistry`**: `deploy.sh` and `provision.sh` reference `dwaregistry` as the default ACR name. The live cluster uses `vnvregistry`. Both are wrong by this convention; the target is `acrdwavv`. This is tracked in the deploy script comments and runbook but not yet corrected.

2. **`deploy-azure.yml` workflow name**: says "Build and deploy to AKS (VnV-Project)" but actually deploys to App Service. Corrected in the diff above.

3. **`HELM_NAMESPACE: default`**: the active workflow deploys into the `default` namespace. This is a security anti-pattern (RBAC bypass). Corrected in the diff above. Migration requires a planned maintenance window.

4. **Full 40-char SHA image tags**: the active workflow uses `${{ github.sha }}` (40 chars) as the image tag. Corrected to 7-char short SHA in the diff above.
