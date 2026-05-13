#!/usr/bin/env bash
# Tears down the AKS testing environment provisioned by deploy/aks/provision.sh.
#
# Deletes the entire resource group in one shot — AKS, ACR, Azure SQL, Key Vault,
# Log Analytics, Managed Identity, all of it. Costs stop accruing once deletion
# completes (~10-15 minutes for AKS + dependent resources to fully drain).
#
# Requires: az CLI (az login).
# Usage:    ./deploy/aks/teardown.sh
#
# Default RG matches provision.sh: rg-dwa-vv-aks. Override with AZ_RG=... if you
# changed it during provisioning.
#
# SAFETY: Key Vault has soft-delete on by default. After teardown the KV name
# remains reserved for ~90 days. To reuse the same name immediately, run:
#   az keyvault purge --name $AZ_KV --no-wait
# (Be very sure — purge is permanent.)

set -euo pipefail

: "${AZ_RG:=rg-dwa-vv-aks}"
: "${AZ_KV:=kv-dwa-vv-aks}"
: "${PURGE_KEYVAULT:=false}"   # set to "true" to also purge soft-deleted KV

echo "==> Subscription: $(az account show --query name -o tsv)"
echo "==> Resource group to delete: $AZ_RG"
echo

# Confirm the RG exists before we wait on a no-op delete.
if ! az group show -n "$AZ_RG" -o none 2>/dev/null; then
  echo "Resource group $AZ_RG does not exist. Nothing to tear down."
  exit 0
fi

echo "==> Listing what's about to be deleted:"
az resource list -g "$AZ_RG" --query "[].{name:name,type:type}" -o table || true
echo

read -rp "Type the resource group name ($AZ_RG) to confirm deletion: " CONFIRM
if [[ "$CONFIRM" != "$AZ_RG" ]]; then
  echo "Confirmation didn't match. Aborting — nothing deleted."
  exit 1
fi

echo "==> Deleting resource group $AZ_RG (this runs in the background)..."
az group delete -n "$AZ_RG" --yes --no-wait
echo "Background deletion started. Costs stop accruing in ~10-15 minutes."
echo "Track with:  az group show -n $AZ_RG --query properties.provisioningState -o tsv"
echo "                                                         (returns 'Deleting' until done)"

if [[ "$PURGE_KEYVAULT" == "true" ]]; then
  echo
  echo "==> PURGE_KEYVAULT=true — also purging soft-deleted Key Vault $AZ_KV..."
  echo "    (This is permanent — the KV name becomes available immediately.)"
  # Wait for soft-delete to complete first (~30 seconds), then purge.
  sleep 30
  az keyvault purge --name "$AZ_KV" --no-wait || \
    echo "    (KV may not have hit soft-deleted state yet — re-run 'az keyvault purge --name $AZ_KV' later if needed.)"
fi

echo
echo "==> Teardown initiated. Run 'az group show -n $AZ_RG' to track progress."
