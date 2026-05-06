#!/usr/bin/env bash
# Provisions an AKS cluster + supporting Azure resources for the DWA V&V System.
#
# This is the STAGE 3+ infrastructure path. For the current demo/testing
# environment use App Service (deploy/provision-azure.sh).
#
# Requires: az CLI (az login), kubectl, helm, openssl.
# Approximate cost (South Africa North region):
#   AKS Standard tier control plane:  ~$0.10/hr = ~$72/month
#   3x Standard_B2s nodes:            ~$45/month
#   Azure SQL Basic:                  ~$5/month
#   Total baseline:                   ~$120+/month (before bandwidth/storage)
#
# Usage:
#   export AKS_CLUSTER=aks-dwa-vv-prod
#   ./deploy/aks/provision.sh

set -euo pipefail

# ---------- Configurable ----------
: "${AZ_LOCATION:=southafricanorth}"
: "${AZ_RG:=rg-dwa-vv-aks}"
: "${AZ_ACR:=dwaregistry}"                     # must be globally unique
: "${AKS_CLUSTER:=aks-dwa-vv-demo}"
: "${AKS_NODE_COUNT:=2}"
: "${AKS_NODE_VM:=Standard_B2s}"               # 2 vCPU, 4 GB RAM
: "${AZ_SQL_SERVER:=sql-dwa-vv-aks}"
: "${AZ_SQL_DB:=dwa_val_ver}"
: "${AZ_SQL_ADMIN:=dwaadmin}"
: "${AZ_SQL_PASSWORD:=$(openssl rand -base64 24 | tr -d '/+=' | head -c 24)Aa1!}"
: "${AZ_KV:=kv-dwa-vv-aks}"
: "${AZ_LAW:=law-dwa-vv}"                      # Log Analytics workspace
: "${K8S_NAMESPACE:=dwa-vv}"
# POPIA acknowledgement value — set to "true" for demo/testing.
# Remove this KV secret entry when Task 10.3 (DataProtection) lands.
: "${IDENTITY_DEMO_PASSWORD:=Demo@Pass2026}"
# ----------------------------------

echo "==> Subscription: $(az account show --query name -o tsv)"
echo "==> Resource group: $AZ_RG ($AZ_LOCATION)"
echo

# 1. Resource Group
az group create -n "$AZ_RG" -l "$AZ_LOCATION" -o none
echo "Resource group $AZ_RG ready."

# 2. Log Analytics Workspace (needed before AKS for Container Insights)
echo "==> Creating Log Analytics workspace..."
AZ_LAW_ID=$(az monitor log-analytics workspace create \
  -g "$AZ_RG" -n "$AZ_LAW" -l "$AZ_LOCATION" \
  --query id -o tsv)

# 3. Azure Container Registry (no admin user — AKS uses managed identity attach)
echo "==> Creating ACR $AZ_ACR..."
az acr create \
  -g "$AZ_RG" -n "$AZ_ACR" -l "$AZ_LOCATION" \
  --sku Basic --admin-enabled false -o none

ACR_ID=$(az acr show -g "$AZ_RG" -n "$AZ_ACR" --query id -o tsv)

# 4. AKS Cluster with:
#    - Azure Workload Identity (OIDC issuer + workload identity)
#    - Azure CNI Overlay networking
#    - Container Insights
#    - Secrets Store CSI driver add-on
echo "==> Creating AKS cluster $AKS_CLUSTER (this takes ~5 minutes)..."
az aks create \
  -g "$AZ_RG" \
  -n "$AKS_CLUSTER" \
  -l "$AZ_LOCATION" \
  --node-count "$AKS_NODE_COUNT" \
  --node-vm-size "$AKS_NODE_VM" \
  --network-plugin azure \
  --network-plugin-mode overlay \
  --enable-oidc-issuer \
  --enable-workload-identity \
  --enable-addons monitoring \
  --workspace-resource-id "$AZ_LAW_ID" \
  --attach-acr "$ACR_ID" \
  --generate-ssh-keys \
  --tier free \
  -o none

echo "==> Fetching kubeconfig..."
az aks get-credentials -g "$AZ_RG" -n "$AKS_CLUSTER" --overwrite-existing

# Install Secrets Store CSI Driver add-on (if not already via --enable-addons)
echo "==> Enabling Secrets Store CSI Driver add-on..."
az aks enable-addons \
  -g "$AZ_RG" -n "$AKS_CLUSTER" \
  --addons azure-keyvault-secrets-provider \
  -o none || echo "(CSI driver may already be enabled)"

# 5. Azure SQL
echo "==> Creating Azure SQL server + database..."
az sql server create \
  -g "$AZ_RG" -n "$AZ_SQL_SERVER" -l "$AZ_LOCATION" \
  --admin-user "$AZ_SQL_ADMIN" --admin-password "$AZ_SQL_PASSWORD" -o none

az sql db create \
  -g "$AZ_RG" -s "$AZ_SQL_SERVER" -n "$AZ_SQL_DB" \
  --service-objective Basic -o none

az sql server firewall-rule create \
  -g "$AZ_RG" -s "$AZ_SQL_SERVER" -n AllowAzureServices \
  --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 -o none

MY_IP="$(curl -s https://api.ipify.org || true)"
if [[ -n "$MY_IP" ]]; then
  az sql server firewall-rule create \
    -g "$AZ_RG" -s "$AZ_SQL_SERVER" -n AllowWorkstation \
    --start-ip-address "$MY_IP" --end-ip-address "$MY_IP" -o none
fi

CONN_STR="Server=tcp:${AZ_SQL_SERVER}.database.windows.net,1433;Initial Catalog=${AZ_SQL_DB};User Id=${AZ_SQL_ADMIN};Password=${AZ_SQL_PASSWORD};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

# 6. Key Vault
echo "==> Creating Key Vault $AZ_KV..."
az keyvault create \
  -g "$AZ_RG" -n "$AZ_KV" -l "$AZ_LOCATION" \
  --enable-rbac-authorization true \
  -o none

KV_ID=$(az keyvault show -g "$AZ_RG" -n "$AZ_KV" --query id -o tsv)

# Grant current user admin rights on KV so we can set secrets
CURRENT_USER=$(az ad signed-in-user show --query id -o tsv)
az role assignment create \
  --role "Key Vault Administrator" \
  --assignee "$CURRENT_USER" \
  --scope "$KV_ID" -o none

# Store secrets in Key Vault
# Secret name format: uses -- as separator (maps to : in ASP.NET Core config)
echo "==> Storing secrets in Key Vault..."
az keyvault secret set \
  --vault-name "$AZ_KV" \
  --name "ConnectionStrings--Default" \
  --value "$CONN_STR" -o none

az keyvault secret set \
  --vault-name "$AZ_KV" \
  --name "Identity--InitialDemoPassword" \
  --value "$IDENTITY_DEMO_PASSWORD" -o none

az keyvault secret set \
  --vault-name "$AZ_KV" \
  --name "Portal--AllowPlaintextIdentityNumber" \
  --value "true" -o none
# REMOVE the above secret when Task 10.3 (DataProtection encryption) is complete.

# 7. Workload Identity for the app pod
echo "==> Creating managed identity for app workload..."
APP_MI_NAME="mi-dwa-vv-app"
az identity create -g "$AZ_RG" -n "$APP_MI_NAME" -l "$AZ_LOCATION" -o none
APP_MI_CLIENT_ID=$(az identity show -g "$AZ_RG" -n "$APP_MI_NAME" --query clientId -o tsv)
APP_MI_PRINCIPAL_ID=$(az identity show -g "$AZ_RG" -n "$APP_MI_NAME" --query principalId -o tsv)

# Grant managed identity "Key Vault Secrets User" on the vault
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee "$APP_MI_PRINCIPAL_ID" \
  --scope "$KV_ID" -o none

# Federate the managed identity with the K8s service account
AKS_OIDC_ISSUER=$(az aks show -g "$AZ_RG" -n "$AKS_CLUSTER" --query oidcIssuerProfile.issuerUrl -o tsv)

az identity federated-credential create \
  -g "$AZ_RG" \
  --identity-name "$APP_MI_NAME" \
  --name "dwa-vv-aks-fed" \
  --issuer "$AKS_OIDC_ISSUER" \
  --subject "system:serviceaccount:${K8S_NAMESPACE}:dwa-vv-dwa-vv" \
  --audience api://AzureADTokenExchange \
  -o none

# 8. Create K8s namespace
kubectl create namespace "$K8S_NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -

echo
echo "==============================================================="
echo "AKS provisioning complete."
echo
echo "ACR login server:          ${AZ_ACR}.azurecr.io"
echo "AKS cluster:               $AKS_CLUSTER"
echo "Key Vault:                 $AZ_KV"
echo "Managed Identity clientId: $APP_MI_CLIENT_ID"
echo "SQL admin password:        $AZ_SQL_PASSWORD"
echo
echo "Next: run ./deploy/aks/deploy.sh"
echo "  Required env vars:"
echo "    export ACR_NAME=$AZ_ACR"
echo "    export AZ_KV=$AZ_KV"
echo "    export MANAGED_IDENTITY_CLIENT_ID=$APP_MI_CLIENT_ID"
echo "    export AZURE_TENANT_ID=$(az account show --query tenantId -o tsv)"
echo "    export INGRESS_HOST=<your-dns-name>"
echo "==============================================================="
