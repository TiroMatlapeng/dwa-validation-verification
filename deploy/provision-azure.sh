#!/usr/bin/env bash
# Provisions Azure resources for a DWA V&V demo environment.
#
# Requires: az CLI (az login), openssl.
# Usage:    ./deploy/provision-azure.sh
#
# Creates:
#   - Resource Group
#   - Linux App Service Plan (B1, ~R250/month)
#   - Web App on .NET 10
#   - Azure SQL Server + single database (Basic, ~R75/month)
#   - Firewall rule allowing Azure services + the App Service outbound IPs
#   - Sets ConnectionStrings__Default as a Web App setting
#   - Enables HTTPS-only + Always On
#   - Prints the publish profile so it can be pasted into GitHub Secrets.
#
# Tear down with: ./deploy/teardown-azure.sh

set -euo pipefail

# ---------- Configurable ----------
: "${AZ_LOCATION:=southafricanorth}"
: "${AZ_RG:=rg-dwa-vv-demo}"
: "${AZ_PLAN:=plan-dwa-vv-demo}"
: "${AZ_APP:=dwa-vv-demo}"              # must be globally unique on azurewebsites.net
: "${AZ_SQL_SERVER:=sql-dwa-vv-demo}"   # must be globally unique on database.windows.net
: "${AZ_SQL_DB:=dwa_val_ver}"
: "${AZ_SQL_ADMIN:=dwaadmin}"
: "${AZ_SQL_PASSWORD:=$(openssl rand -base64 24 | tr -d '/+=' | head -c 24)Aa1!}"
: "${AZ_RUNTIME:=DOTNETCORE:10.0}"
: "${AZ_PLAN_SKU:=B1}"
: "${AZ_SQL_SKU:=Basic}"
# ----------------------------------

echo "==> Using subscription: $(az account show --query name -o tsv)"
echo "==> Resource group:     $AZ_RG ($AZ_LOCATION)"
echo "==> Web app:            https://${AZ_APP}.azurewebsites.net"
echo "==> SQL server:         ${AZ_SQL_SERVER}.database.windows.net"
echo

az group create -n "$AZ_RG" -l "$AZ_LOCATION" -o none

echo "==> Creating App Service plan ($AZ_PLAN_SKU, Linux)..."
az appservice plan create \
  -g "$AZ_RG" -n "$AZ_PLAN" \
  --is-linux --sku "$AZ_PLAN_SKU" -o none

echo "==> Creating Web App..."
az webapp create \
  -g "$AZ_RG" -p "$AZ_PLAN" -n "$AZ_APP" \
  --runtime "$AZ_RUNTIME" -o none

az webapp update -g "$AZ_RG" -n "$AZ_APP" --https-only true -o none
az webapp config set -g "$AZ_RG" -n "$AZ_APP" --always-on true -o none

echo "==> Creating Azure SQL server + database..."
az sql server create \
  -g "$AZ_RG" -n "$AZ_SQL_SERVER" -l "$AZ_LOCATION" \
  --admin-user "$AZ_SQL_ADMIN" --admin-password "$AZ_SQL_PASSWORD" -o none

az sql db create \
  -g "$AZ_RG" -s "$AZ_SQL_SERVER" -n "$AZ_SQL_DB" \
  --service-objective "$AZ_SQL_SKU" -o none

echo "==> Allowing Azure services to reach SQL server..."
az sql server firewall-rule create \
  -g "$AZ_RG" -s "$AZ_SQL_SERVER" -n AllowAzureServices \
  --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 -o none

# Allow your current workstation IP so you can run migrations / connect from SSMS
MY_IP="$(curl -s https://api.ipify.org || true)"
if [[ -n "$MY_IP" ]]; then
  az sql server firewall-rule create \
    -g "$AZ_RG" -s "$AZ_SQL_SERVER" -n AllowMyWorkstation \
    --start-ip-address "$MY_IP" --end-ip-address "$MY_IP" -o none
  echo "    Added workstation IP $MY_IP to firewall."
fi

CONN_STR="Server=tcp:${AZ_SQL_SERVER}.database.windows.net,1433;Initial Catalog=${AZ_SQL_DB};User Id=${AZ_SQL_ADMIN};Password=${AZ_SQL_PASSWORD};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

echo "==> Setting connection string on Web App..."
az webapp config connection-string set \
  -g "$AZ_RG" -n "$AZ_APP" \
  --connection-string-type SQLAzure \
  --settings "Default=${CONN_STR}" -o none

az webapp config appsettings set \
  -g "$AZ_RG" -n "$AZ_APP" \
  --settings ASPNETCORE_ENVIRONMENT=Production -o none

echo
echo "==============================================================="
echo "Provisioning complete."
echo
echo "SQL admin password (save this somewhere safe):"
echo "  $AZ_SQL_PASSWORD"
echo
echo "Web app URL:"
echo "  https://${AZ_APP}.azurewebsites.net"
echo
echo "Next step — add the publish profile to GitHub as the"
echo "AZURE_WEBAPP_PUBLISH_PROFILE secret. Run:"
echo
echo "  az webapp deployment list-publishing-profiles \\"
echo "    -g $AZ_RG -n $AZ_APP --xml"
echo
echo "Copy the XML output and paste it into:"
echo "  GitHub repo → Settings → Secrets and variables → Actions → New secret"
echo "  Name:  AZURE_WEBAPP_PUBLISH_PROFILE"
echo "==============================================================="
