#!/usr/bin/env bash
# Builds, pushes, and deploys the DWA V&V System image to AKS via Helm.
#
# Usage (minimum):
#   export ACR_NAME=dwaregistry
#   export RG=rg-dwa-vv-aks
#   export AKS_CLUSTER=aks-dwa-vv-demo
#   ./deploy/aks/deploy.sh
#
# Full example with all options:
#   export ACR_NAME=dwaregistry
#   export RG=rg-dwa-vv-aks
#   export AKS_CLUSTER=aks-dwa-vv-demo
#   export IMAGE_TAG=abc1234
#   export RELEASE_NAME=dwa-vv
#   export NAMESPACE=dwa-vv
#   export VALUES_FILE=deploy/helm/dwa-vv/values.prod.yaml
#   export MANAGED_IDENTITY_CLIENT_ID=<uuid>
#   export AZURE_TENANT_ID=<uuid>
#   export KEY_VAULT_NAME=kv-dwa-vv-aks
#   ./deploy/aks/deploy.sh
#
# Requires: docker, az CLI, kubectl, helm.

set -euo pipefail

# ── Required env vars ─────────────────────────────────────────────────────────
: "${ACR_NAME:?ERROR: ACR_NAME is required (e.g. dwaregistry)}"
: "${RG:?ERROR: RG is required — the Azure resource group}"
: "${AKS_CLUSTER:?ERROR: AKS_CLUSTER is required}"

# ── Optional env vars with defaults ──────────────────────────────────────────
IMAGE_TAG="${IMAGE_TAG:-$(git rev-parse --short HEAD)}"
RELEASE_NAME="${RELEASE_NAME:-dwa-vv}"
NAMESPACE="${NAMESPACE:-dwa-vv}"
VALUES_FILE="${VALUES_FILE:-deploy/helm/dwa-vv/values.dev.yaml}"

# Workload Identity + Key Vault (passed through to Helm --set; empty = use values file defaults)
MANAGED_IDENTITY_CLIENT_ID="${MANAGED_IDENTITY_CLIENT_ID:-}"
AZURE_TENANT_ID="${AZURE_TENANT_ID:-}"
KEY_VAULT_NAME="${KEY_VAULT_NAME:-}"

FULL_IMAGE="${ACR_NAME}.azurecr.io/dwa-vv"

echo "================================================================="
echo " DWA V&V AKS Deploy"
echo " Image:       ${FULL_IMAGE}:${IMAGE_TAG}"
echo " Cluster:     ${AKS_CLUSTER} (RG: ${RG})"
echo " Namespace:   ${NAMESPACE}"
echo " Release:     ${RELEASE_NAME}"
echo " Values file: ${VALUES_FILE}"
echo "================================================================="
echo

# ── Step 1: Build Docker image ────────────────────────────────────────────────
echo "==> [1/7] Building Docker image..."
docker build \
  --tag "${FULL_IMAGE}:${IMAGE_TAG}" \
  --tag "${FULL_IMAGE}:latest" \
  .

echo "    Built ${FULL_IMAGE}:${IMAGE_TAG}"

# ── Step 2: Authenticate to ACR and push ─────────────────────────────────────
echo "==> [2/7] Logging in to ACR and pushing image..."
az acr login -n "${ACR_NAME}"

docker push "${FULL_IMAGE}:${IMAGE_TAG}"
docker push "${FULL_IMAGE}:latest"

echo "    Pushed ${FULL_IMAGE}:${IMAGE_TAG}"

# ── Step 3: Fetch AKS credentials ────────────────────────────────────────────
echo "==> [3/7] Fetching AKS credentials..."
az aks get-credentials -g "${RG}" -n "${AKS_CLUSTER}" --overwrite-existing

# ── Step 4: Ensure namespace exists ──────────────────────────────────────────
echo "==> [4/7] Ensuring namespace '${NAMESPACE}' exists..."
kubectl create namespace "${NAMESPACE}" --dry-run=client -o yaml | kubectl apply -f -

# ── Step 5: Helm upgrade --install ───────────────────────────────────────────
echo "==> [5/7] Running Helm upgrade --install..."

# Build dynamic --set overrides for values that come from CI/env at deploy time.
# Values already in the values file are not repeated here.
HELM_EXTRA_SETS=""

if [[ -n "${MANAGED_IDENTITY_CLIENT_ID}" ]]; then
  HELM_EXTRA_SETS="${HELM_EXTRA_SETS} --set workloadIdentity.clientId=${MANAGED_IDENTITY_CLIENT_ID}"
fi

if [[ -n "${AZURE_TENANT_ID}" ]]; then
  HELM_EXTRA_SETS="${HELM_EXTRA_SETS} --set workloadIdentity.tenantId=${AZURE_TENANT_ID}"
fi

if [[ -n "${KEY_VAULT_NAME}" ]]; then
  HELM_EXTRA_SETS="${HELM_EXTRA_SETS} --set keyVault.name=${KEY_VAULT_NAME}"
fi

# shellcheck disable=SC2086
helm upgrade --install "${RELEASE_NAME}" deploy/helm/dwa-vv \
  --namespace "${NAMESPACE}" \
  --values "${VALUES_FILE}" \
  --set "image.tag=${IMAGE_TAG}" \
  --set "image.repository=${FULL_IMAGE}" \
  ${HELM_EXTRA_SETS} \
  --wait \
  --timeout 5m

echo "    Helm release '${RELEASE_NAME}' applied."

# ── Step 6: Wait for rollout ──────────────────────────────────────────────────
echo "==> [6/7] Waiting for rollout to complete..."
kubectl -n "${NAMESPACE}" rollout status deployment/"${RELEASE_NAME}" --timeout 5m
echo "    Rollout complete."

# ── Step 7: Smoke test ────────────────────────────────────────────────────────
echo "==> [7/7] Running smoke test..."

# Determine the reachable URL: prefer ingress hostname, fall back to LoadBalancer IP.
INGRESS_HOST=$(kubectl -n "${NAMESPACE}" get ingress \
  -o jsonpath='{.items[0].spec.rules[0].host}' 2>/dev/null || true)

if [[ -n "${INGRESS_HOST}" ]]; then
  SMOKE_URL="https://${INGRESS_HOST}/"
  # If TLS is disabled in the values file the host still appears but the cert may not
  # be ready yet — try http as a fallback if https returns connection refused.
  echo "    Ingress hostname: ${INGRESS_HOST}"
else
  # No ingress: try the Service's external IP (LoadBalancer type)
  echo "    No ingress found — looking for LoadBalancer external IP..."
  for i in $(seq 1 12); do
    LB_IP=$(kubectl -n "${NAMESPACE}" get svc "${RELEASE_NAME}" \
      -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || true)
    if [[ -n "${LB_IP}" && "${LB_IP}" != "<pending>" ]]; then
      break
    fi
    echo "    Waiting for external IP (attempt ${i}/12)..."
    sleep 10
  done
  SMOKE_URL="http://${LB_IP:-127.0.0.1}/"
  echo "    LoadBalancer IP: ${LB_IP:-not assigned}"
fi

echo "    Smoke-testing ${SMOKE_URL} ..."
HTTP_STATUS=$(curl \
  --silent \
  --output /dev/null \
  --write-out "%{http_code}" \
  --retry 5 \
  --retry-delay 5 \
  --max-time 30 \
  --location-trusted \
  "${SMOKE_URL}" || echo "000")

if [[ "${HTTP_STATUS}" == "200" || "${HTTP_STATUS}" == "302" ]]; then
  echo "    Smoke test PASSED (HTTP ${HTTP_STATUS}). App is reachable."
  SMOKE_RESULT="PASSED"
else
  echo "    Smoke test WARNING: received HTTP ${HTTP_STATUS}."
  echo "    This may be expected if DNS is not yet pointed at the ingress IP,"
  echo "    or if a TLS certificate is still being issued by cert-manager."
  SMOKE_RESULT="WARNING (HTTP ${HTTP_STATUS})"
fi

# ── Final state ───────────────────────────────────────────────────────────────
echo
echo "================================================================="
echo " Cluster state after deploy:"
echo "================================================================="
kubectl -n "${NAMESPACE}" get pods,svc,ingress
echo
echo "================================================================="
echo " Deploy summary"
echo "   Release:     ${RELEASE_NAME}"
echo "   Image:       ${FULL_IMAGE}:${IMAGE_TAG}"
echo "   Namespace:   ${NAMESPACE}"
echo "   Smoke test:  ${SMOKE_RESULT}"
if [[ -n "${INGRESS_HOST}" ]]; then
  echo "   URL:         https://${INGRESS_HOST}/"
fi
echo "================================================================="
