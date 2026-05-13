#!/usr/bin/env bash
# Deletes the entire DWA V&V demo resource group. Irreversible.
set -euo pipefail
: "${AZ_RG:=rg-dwa-vv-demo}"

read -r -p "Delete resource group '$AZ_RG' and everything in it? [y/N] " ans
[[ "$ans" =~ ^[Yy]$ ]] || { echo "Aborted."; exit 1; }

az group delete -n "$AZ_RG" --yes --no-wait
echo "Delete started (running in background)."
