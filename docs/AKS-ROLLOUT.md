# AKS Rollout Runbook — DWA V&V System

> **Status (2026-05-03):** AKS is the Stage-3+ path. The immediate demo (2026-05-06) uses
> App Service — see `.github/workflows/deploy-azure.yml`. AKS becomes the active path
> once Stage 2b lands DataProtection key persistence and a real email sender.

---

## Prerequisites (one-time)

### Azure

- Active Azure subscription with billing enabled.
- Your Azure account must have one of these RBAC roles (or equivalent) to create all resources:
  - **Contributor** on the subscription, **or**
  - **Owner** on a dedicated resource group you create first.
- The subscription must have the following resource providers registered:
  ```
  az provider register --namespace Microsoft.ContainerService   # AKS
  az provider register --namespace Microsoft.ContainerRegistry  # ACR
  az provider register --namespace Microsoft.Sql                # Azure SQL
  az provider register --namespace Microsoft.KeyVault
  az provider register --namespace Microsoft.ManagedIdentity
  az provider register --namespace Microsoft.OperationalInsights
  ```

### Local tooling

| Tool | Minimum version | Install |
|------|----------------|---------|
| `az` CLI | 2.57+ | `brew install azure-cli` / [docs.microsoft.com/cli](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) |
| `kubectl` | 1.29+ | `az aks install-cli` or `brew install kubectl` |
| `helm` | 3.14+ | `brew install helm` |
| `docker` | any recent | Docker Desktop |

Run `az login` before any provisioning step.

### GitHub repo secrets and variables

Set these once in **GitHub → Settings → Secrets and variables → Actions**:

| Key | Type | Value |
|-----|------|-------|
| `AZURE_CREDENTIALS` | **Secret** | Service principal JSON from `az ad sp create-for-rbac` (see below) |
| `ACR_NAME` | Variable | The ACR name you chose (e.g. `dwaregistry`) |
| `AZURE_RG` | Variable | Resource group name (e.g. `rg-dwa-vv-aks`) |
| `AKS_CLUSTER_NAME` | Variable | AKS cluster name (e.g. `aks-dwa-vv-demo`) |
| `MANAGED_IDENTITY_CLIENT_ID` | Variable | From provisioning output |
| `AZURE_TENANT_ID` | Variable | Your Azure AD tenant ID |
| `KEY_VAULT_NAME` | Variable | Key Vault name (e.g. `kv-dwa-vv-aks`) |

**Creating the service principal for `AZURE_CREDENTIALS`:**

```bash
az ad sp create-for-rbac \
  --name sp-dwa-vv-github \
  --role Contributor \
  --scopes /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/rg-dwa-vv-aks \
  --sdk-auth
```

Copy the JSON output verbatim as the `AZURE_CREDENTIALS` secret value. The SP also needs
`AcrPush` on the ACR and `Azure Kubernetes Service Cluster User Role` on the AKS cluster —
grant these after provisioning completes.

---

## First-time provisioning

The provisioning script creates: resource group, Log Analytics workspace, ACR, AKS cluster
(with OIDC issuer, Workload Identity, Container Insights, and Secrets Store CSI add-on),
Azure SQL server + database, Key Vault (RBAC mode), and a managed identity with federated
credentials for the app pod.

**Expected duration:** 15–20 minutes (AKS cluster creation dominates).

```bash
export AZ_LOCATION=southafricanorth   # or eastus / westeurope
export AZ_RG=rg-dwa-vv-aks
export AZ_ACR=dwaregistry            # globally unique name
export AKS_CLUSTER=aks-dwa-vv-demo
export AZ_KV=kv-dwa-vv-aks
export AZ_SQL_SERVER=sql-dwa-vv-aks

bash deploy/aks/provision.sh
```

All other variables have sensible defaults. The SQL admin password is auto-generated and
printed at the end — save it to a password manager immediately.

**What you get at the end:**

```
ACR login server:          dwaregistry.azurecr.io
AKS cluster:               aks-dwa-vv-demo
Key Vault:                 kv-dwa-vv-aks
Managed Identity clientId: <uuid>
SQL admin password:        <generated>
```

**Confirm provisioning succeeded:**

```bash
# AKS nodes ready
kubectl get nodes

# CSI driver pods running
kubectl get pods -n kube-system -l app=secrets-store-csi-driver

# Key Vault secrets present
az keyvault secret list --vault-name kv-dwa-vv-aks -o table
```

---

## First deploy

After provisioning, export the values printed at the end of `provision.sh`:

```bash
export ACR_NAME=dwaregistry
export RG=rg-dwa-vv-aks
export AKS_CLUSTER=aks-dwa-vv-demo
export MANAGED_IDENTITY_CLIENT_ID=<uuid from provisioning output>
export AZURE_TENANT_ID=$(az account show --query tenantId -o tsv)
export KEY_VAULT_NAME=kv-dwa-vv-aks

# Optional overrides (defaults shown):
# export IMAGE_TAG=$(git rev-parse --short HEAD)
# export RELEASE_NAME=dwa-vv
# export NAMESPACE=dwa-vv
# export VALUES_FILE=deploy/helm/dwa-vv/values.dev.yaml

bash deploy/aks/deploy.sh
```

The script will:
1. Build the Docker image from the repo root.
2. Push it to ACR.
3. Fetch AKS credentials (`~/.kube/config` is updated).
4. Create the `dwa-vv` namespace if absent.
5. Run `helm upgrade --install` — applying the chart and waiting for pods to be Ready.
6. Wait for the Deployment rollout to complete.
7. Smoke-test the ingress or LoadBalancer URL (expects HTTP 200 or 302).
8. Print final pod/service/ingress state.

**Verifying the smoke test:**

- `HTTP 200` — app served the home page.
- `HTTP 302` — app redirected to `/Account/Login`; this is correct, the app is running.
- `HTTP 000` or connection refused — DNS not yet pointed, or cert-manager is still issuing the TLS certificate. Check `kubectl -n dwa-vv describe certificate dwa-vv-tls`.

**Reading the ingress hostname:**

```bash
kubectl -n dwa-vv get ingress
```

The `ADDRESS` column shows the public IP of the NGINX ingress controller. Point your DNS
A record at that IP, then update `ingress.host` in `values.prod.yaml`.

---

## IP-only testing path (no domain, no TLS) — the Stage 2a default

`values.dev.yaml` is configured for this path — `ingress.enabled: false` and
`service.type: LoadBalancer`. After `deploy.sh` finishes, AKS provisions an Azure
public IP and assigns it to the Service. **NGINX ingress and cert-manager are not
required** for this flow.

Get the public IP:

```bash
kubectl -n dwa-vv get svc dwa-vv
# NAME     TYPE           CLUSTER-IP    EXTERNAL-IP    PORT(S)
# dwa-vv   LoadBalancer   10.0.X.Y      <PENDING>      80:32xxx/TCP
#
# Wait 1-3 minutes for Azure to assign the public IP, then:
# EXTERNAL-IP shows e.g. 4.231.X.Y
```

Browse to `http://<EXTERNAL-IP>/ExternalPortal/Account/Register` to test the portal.

**Critical caveat — login won't work over a raw HTTP IP.** The portal cookie is
configured with `SecurePolicy = Always` for production safety. Browsers won't
accept the cookie over plain HTTP, so the post-login redirect lands you back on
the login page in a loop. Two workable testing flows:

1. **Recommended for tomorrow's smoke: `kubectl port-forward`.** This is the
   simplest path that exercises the full register → confirm → login → dashboard
   flow over `http://localhost`, where browsers DO accept cookies without TLS:

   ```bash
   kubectl -n dwa-vv port-forward svc/dwa-vv 8080:80
   # Open http://localhost:8080/ExternalPortal/Account/Register in your browser.
   # Click through the demo confirm link, log in, see the dashboard.
   ```

   This proves the AKS pod is healthy AND the full flow works. The public
   `EXTERNAL-IP` is still useful as a "is the LoadBalancer routing correctly"
   smoke (`curl http://<EXTERNAL-IP>/` should return 302 to the login URL),
   it just can't sustain a logged-in session over HTTP.

2. **Browse the public IP for unauthenticated views only.** The Register form,
   Login form, and AccessDenied page all render fine over HTTP — you just
   can't complete a login. Useful for screenshotting the UI in front of
   stakeholders without needing the full flow to work.

When a real domain lands, switch to `values.prod.yaml`, point DNS at the
ingress controller's IP, install cert-manager + cluster issuer, and the
TLS-protected flow works end-to-end with no port-forward.

---

## Tearing down (cost control)

AKS + 2 nodes + ACR + SQL + KV runs ~R1,100/month minimum. Tear down
after every testing session:

```bash
bash deploy/aks/teardown.sh
# Prompts you to type the resource group name to confirm.
# Deletes the entire RG in the background; costs stop accruing in
# ~10-15 minutes once Azure finishes.
```

**Re-provisioning the same Key Vault name:** Azure soft-deletes the KV when
the RG is dropped, reserving the name for ~90 days. To reuse the name
immediately, run teardown with `PURGE_KEYVAULT=true bash deploy/aks/teardown.sh`
or manually `az keyvault purge --name kv-dwa-vv-aks` after teardown completes.

**Tracking deletion progress:**

```bash
az group show -n rg-dwa-vv-aks --query properties.provisioningState -o tsv
# Returns "Deleting" until done; "ResourceGroupNotFound" once complete.
```

---

## DNS and TLS

1. Get the ingress controller's external IP:
   ```bash
   kubectl -n ingress-nginx get svc ingress-nginx-controller
   # or, if using AKS App Routing add-on:
   kubectl -n app-routing-system get svc nginx
   ```

2. Create a DNS A record pointing your domain (e.g. `dwa-vv.yourdomain.gov.za`) at that IP.

3. Update `deploy/helm/dwa-vv/values.prod.yaml`:
   ```yaml
   ingress:
     host: dwa-vv.yourdomain.gov.za
     tls:
       enabled: true
       secretName: dwa-vv-prod-tls
   ```

4. TLS is handled automatically by cert-manager + Let's Encrypt. The chart references the
   cluster issuer `letsencrypt-prod` (annotation `cert-manager.io/cluster-issuer: letsencrypt-prod`).
   This cluster issuer is **not** installed by `provision.sh` — install it once per cluster:

   ```bash
   # Install cert-manager
   helm repo add jetstack https://charts.jetstack.io
   helm repo update
   helm upgrade --install cert-manager jetstack/cert-manager \
     --namespace cert-manager --create-namespace \
     --set installCRDs=true

   # Create the ClusterIssuer
   cat <<EOF | kubectl apply -f -
   apiVersion: cert-manager.io/v1
   kind: ClusterIssuer
   metadata:
     name: letsencrypt-prod
   spec:
     acme:
       server: https://acme-v02.api.letsencrypt.org/directory
       email: <your-email>
       privateKeySecretRef:
         name: letsencrypt-prod
       solvers:
         - http01:
             ingress:
               class: nginx
   EOF
   ```

   Full cert-manager docs: https://cert-manager.io/docs/installation/helm/

5. Let's Encrypt requires the domain to be publicly reachable on port 80 for the ACME
   HTTP-01 challenge. Allow a few minutes for certificate issuance after DNS propagates.

---

## Day-2 operations

### View logs

```bash
# All pods in the namespace
kubectl -n dwa-vv logs -l app.kubernetes.io/name=dwa-vv --all-containers=true --follow

# Specific pod
kubectl -n dwa-vv get pods
kubectl -n dwa-vv logs <pod-name>
```

### Describe / diagnose a stuck pod

```bash
kubectl -n dwa-vv describe pod <pod-name>
kubectl -n dwa-vv get events --sort-by=.lastTimestamp
```

### Scaling

The HPA (min 2, max 5 in dev; min 3, max 10 in prod) handles automatic scaling. No manual
action is usually required. To check HPA status:

```bash
kubectl -n dwa-vv get hpa
kubectl -n dwa-vv describe hpa dwa-vv
```

To temporarily override (e.g. for maintenance):

```bash
kubectl -n dwa-vv scale deployment/dwa-vv --replicas=1
```

Re-enable HPA by running a fresh `helm upgrade` (the HPA re-applies its `minReplicas`).

### Rollback

```bash
# List revision history
helm -n dwa-vv history dwa-vv

# Roll back to the previous release
helm -n dwa-vv rollback dwa-vv

# Roll back to a specific revision
helm -n dwa-vv rollback dwa-vv 3
```

The rollback swaps the image tag and config atomically; pods are replaced via RollingUpdate.

### Updating Key Vault secrets

Secrets are synced from Key Vault via the Secrets Store CSI driver. When you update a
secret in Key Vault, the CSI driver will re-sync on its next poll interval (default 2
minutes). No pod restart is needed for the volume mount to refresh, but the environment
variables projected from the synced K8s Secret will only update on the next pod restart.
Force a rolling restart after updating a sensitive secret:

```bash
az keyvault secret set --vault-name kv-dwa-vv-aks \
  --name "ConnectionStrings--Default" --value "<new value>"

# Wait ~2 min for CSI sync, then:
kubectl -n dwa-vv rollout restart deployment/dwa-vv
```

### Checking the SecretProviderClass

```bash
kubectl -n dwa-vv get secretproviderclass
kubectl -n dwa-vv describe secretproviderclass dwa-vv-dwa-vv-kv
```

If a secret is missing in Key Vault, pods will fail to start with
`MountVolume.SetUp failed ... could not get secret`.

---

## Known limitations and pre-Stage-2b blockers

### DataProtection key persistence — SQL-backed, unencrypted at rest (TRACKED)

**Current state (2026-05-21):** Keys are persisted to the `DataProtectionKeys` table in
`dwa_val_ver` via `PersistKeysToDbContext<ApplicationDBContext>()`. This means
multi-replica deployments share the same key ring — email confirmation tokens work correctly
across pods. This replaces the previous in-memory approach.

**Remaining security gap:** The keys are stored as plain XML in the `DataProtectionKeys`
table. Anyone with SQL read access (SA account, any DBA-level query) can extract the key
XML and decrypt any auth cookie or antiforgery token. This is the trust boundary for
the current dev environment.

**Risk assessment (Platform Architect, 2026-05-21):**

This is **acceptable for the dev/demo cluster** because:
- The database contains only seeded demo accounts — no real user data.
- The SQL pod is ClusterIP (not externally accessible).
- The AKS cluster is in a private subscription with Contributor-scoped access.

**Production requirement (before any real user data lands):**

Encrypt the DataProtection keys at rest using Azure Key Vault. The code change is:

```csharp
// Program.cs — replace the current AddDataProtection block with:
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDBContext>()
    .SetApplicationName("dwa-ver-val")
    .ProtectKeysWithAzureKeyVault(
        new Uri($"https://{keyVaultName}.vault.azure.net/keys/data-protection-key"),
        new ManagedIdentityCredential(managedIdentityClientId));
```

This requires:
1. A Key Vault key (not secret) with `Key Vault Crypto User` role on the pod's managed identity.
2. `Microsoft.AspNetCore.DataProtection.AzureKeyVault` NuGet package.
3. Managed identity on the AKS pod (Workload Identity — already plumbed in prod values).

This is tracked as **Task 10.3** in the code comments.

**Option B (if Key Vault is unavailable for prod):** Symmetric key encryption using a
random 32-byte key stored in a K8s secret (`ProtectKeysWithSymmetricEncryption` or a
custom `IXmlEncryptor`). This moves the trust boundary from SQL to the K8s control plane
(etcd). Acceptable as a transition step; not recommended long-term because key rotation
requires coordinated pod restarts.

### Email sender

`LoggingEmailSender` is the active email implementation. Registration confirmation emails
are written to the application log — they do not reach a real inbox.

For click-through testing, use the TempData demo helper link that appears on the
confirmation-sent page (visible in dev/demo mode only).

Wire a real `IEmailSender` (SendGrid / Azure Communication Services) as part of Stage 2b
before any external users attempt registration.

### POPIA plaintext identity number acknowledgement

Key Vault secret `Portal--AllowPlaintextIdentityNumber=true` is intentionally set.
This is a fail-fast acknowledgement that identity numbers are stored without encryption
during this pre-GA phase. The value is consumed by the app startup guard:

```
if (!bool.Parse(config["Portal:AllowPlaintextIdentityNumber"] ?? "false"))
    throw new InvalidOperationException("POPIA guard: IdentityNumber encryption not configured.");
```

Remove this Key Vault secret (or set it to `false`) only after Task 10.3 wires
DataProtection-based encryption for the `IdentityNumber` column.

---

## Cost estimate

All figures are approximate for the **South Africa North** region; verify against the
[Azure Pricing Calculator](https://azure.microsoft.com/en-us/pricing/calculator/).

| Resource | Config | Estimated monthly |
|----------|--------|------------------|
| AKS control plane | Free tier | $0 (Free tier); ~$72 on Standard tier |
| AKS nodes | 2 x Standard_B2s (2 vCPU, 4 GB) | ~$60 |
| Azure Container Registry | Basic SKU | ~$5 |
| Azure SQL | Basic DTU, 5 DTU | ~$5 |
| Log Analytics | Pay-per-use, ~1 GB/day | ~$3–10 |
| **Total (Free AKS tier)** | | **~$73–82/month** |

These figures assume no egress charges, no premium storage, and under 5 GB stored images
in ACR. Egress from South Africa North is billed at standard Azure rates.

---

## App Service vs AKS

The DWA V&V System currently ships via **App Service** for the 2026-05-06 demo. The
App Service workflow (`.github/workflows/deploy-azure.yml`) triggers automatically on
pushes to `main` and `demo/azure-deploy`.

AKS is the target for Stage 3+. It becomes the active production path once:

1. **DataProtection key persistence** is wired (Task 10.3) — required for multi-replica
   correctness of email confirmation tokens.
2. **Real email sender** is configured — required for external portal user registration.
3. The AKS cluster is provisioned and DNS is pointed at the ingress IP.

Until those gates are met, keep `replicaCount: 1` if you run AKS in parallel, and treat
App Service as the authoritative deployment target.
