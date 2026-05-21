# SQL Server Restore Runbook — DWA V&V Dev Cluster

**Cluster:** `vnv-aks` / resource group `VnV-Project` / South Africa North  
**Database:** `dwa_val_ver` running in pod `dwa-vv-mssql` (in-cluster SQL Server 2022)  
**Backup location:** Azure Blob Storage container `mssql-backups`  
**Backup schedule:** Daily at 02:00 UTC (04:00 SAST) via CronJob `dwa-vv-mssql-backup`  
**Retention:** Blob storage retains all backups — no automatic pruning in dev  
**Backup naming:** `dwa_val_ver_YYYYMMDD_HHMMSS.bak`

---

## When to use this runbook

- Accidental data deletion (rows, migrations applied incorrectly)
- Pod PVC corruption or Azure Disk failure
- Post-`helm uninstall` recovery (PV reclaimPolicy=Retain protects the disk, but if the disk is detached/deleted, this runbook is the recovery path)

---

## Step 1 — Get the storage connection string

```bash
# Get the connection string from the K8s secret (set by CI from DEV_BACKUP_STORAGE_CONN_STR)
CONN_STR=$(kubectl get secret dwa-vv-backup-storage -n default \
  -o jsonpath='{.data.connectionString}' | base64 -d)
```

Alternatively, retrieve from Azure Portal: Storage Account → Access keys → Connection string.

---

## Step 2 — List available backups

```bash
az storage blob list \
  --container-name mssql-backups \
  --connection-string "${CONN_STR}" \
  --output table \
  --query "[].{name:name, size:properties.contentLength, modified:properties.lastModified}"
```

Note the blob name, e.g. `dwa_val_ver_20260520_020013.bak`.

---

## Step 3 — Download the target backup

```bash
TARGET_BLOB="dwa_val_ver_20260520_020013.bak"   # replace with target backup
LOCAL_PATH="/tmp/${TARGET_BLOB}"

az storage blob download \
  --container-name mssql-backups \
  --name "${TARGET_BLOB}" \
  --file "${LOCAL_PATH}" \
  --connection-string "${CONN_STR}"
```

---

## Step 4 — Copy the backup into the SQL Server pod

```bash
# Find the mssql pod name
MSSQL_POD=$(kubectl get pods -n default \
  -l "app.kubernetes.io/name=dwa-vv,app.kubernetes.io/component=mssql" \
  -o jsonpath='{.items[0].metadata.name}')

# Create the backup directory inside the pod and copy the file
kubectl exec -n default "${MSSQL_POD}" -- mkdir -p /var/opt/mssql/backup
kubectl cp "${LOCAL_PATH}" "default/${MSSQL_POD}:/var/opt/mssql/backup/${TARGET_BLOB}"

# Verify the copy succeeded
kubectl exec -n default "${MSSQL_POD}" -- ls -lh "/var/opt/mssql/backup/${TARGET_BLOB}"
```

---

## Step 5 — Scale down the application (prevents new writes during restore)

```bash
kubectl scale deployment dwa-vv --replicas=0 -n default
# Wait until no app pods remain
kubectl get pods -n default -l app.kubernetes.io/name=dwa-vv --watch
```

---

## Step 6 — Drop and restore the database via sqlcmd

```bash
# Get the SA password from the K8s secret
SA_PWD=$(kubectl get secret dwa-vv-mssql -n default \
  -o jsonpath='{.data.sa-password}' | base64 -d)

# Restore — drops the existing database and replaces it with the backup
kubectl exec -n default "${MSSQL_POD}" -- \
  /opt/mssql-tools/bin/sqlcmd \
    -S localhost -U sa -P "${SA_PWD}" \
    -C \
    -Q "
      ALTER DATABASE dwa_val_ver SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
      RESTORE DATABASE dwa_val_ver
        FROM DISK = '/var/opt/mssql/backup/${TARGET_BLOB}'
        WITH REPLACE, RECOVERY;
      ALTER DATABASE dwa_val_ver SET MULTI_USER;
      PRINT 'Restore complete.'
    "
```

**Notes:**
- `-C` trusts the self-signed TLS certificate on SQL Server 2022 dev instances
- `WITH REPLACE` overwrites the existing database files
- `WITH RECOVERY` brings the database online immediately (no further log restores needed)

---

## Step 7 — Scale the application back up

```bash
kubectl scale deployment dwa-vv --replicas=1 -n default
kubectl rollout status deployment/dwa-vv -n default --timeout=5m
```

---

## Step 8 — Smoke test

```bash
# Check app responds (via port-forward since service may be ClusterIP)
kubectl port-forward svc/dwa-vv 8080:80 -n default &
curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/
# Expected: 200 or 302 (redirect to login)
kill %1
```

---

## Failure modes

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| `RESTORE failed — exclusive access required` | App pods still running | Scale deployment to 0 before Step 6 |
| `Cannot open backup device '/var/opt/mssql/backup/...'` | File not copied | Verify `kubectl cp` succeeded; check: `kubectl exec ... -- ls /var/opt/mssql/backup/` |
| `Login failed for user 'sa'` | Wrong SA password | Get: `kubectl get secret dwa-vv-mssql -n default -o jsonpath='{.data.sa-password}' \| base64 -d` |
| App crashes after restore | EF migrations ahead of backup | Check: `kubectl exec ... -- sqlcmd ... -Q "SELECT TOP 5 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC"` — may need `dotnet ef database update` to re-apply missing migrations |
| Backup blob not found | Job never ran or DEV_BACKUP_STORAGE_CONN_STR not set | Check CronJob: `kubectl get cronjob dwa-vv-mssql-backup -n default` and job logs |

---

## Checking backup job status

```bash
# List recent backup jobs
kubectl get jobs -n default -l app.kubernetes.io/component=mssql-backup

# Check logs from the most recent backup run
kubectl logs -n default \
  -l app.kubernetes.io/component=mssql-backup \
  --container backup --tail=50

kubectl logs -n default \
  -l app.kubernetes.io/component=mssql-backup \
  --container upload --tail=50
```

---

## Backup architecture note

The CronJob (`deploy/helm/dwa-vv/templates/mssql-backup-cronjob.yaml`) uses a
two-container pattern sharing an `emptyDir` volume:

1. **init container `backup`** (`mcr.microsoft.com/mssql-tools`) — runs `sqlcmd BACKUP DATABASE`
   and writes `dwa_val_ver_YYYYMMDD_HHMMSS.bak` to `/backup/` on the shared volume.
2. **init container `upload`** (`mcr.microsoft.com/azure-cli`) — uploads the `.bak` file
   to Azure Blob Storage using `AZURE_STORAGE_CONNECTION_STRING` from the
   `dwa-vv-backup-storage` K8s secret, then deletes the local file.
3. **main container `done`** (busybox) — trivial "job complete" container required by
   Kubernetes Job spec.

`mcr.microsoft.com/mssql-tools` does NOT include `az` CLI — this is why two containers
are required. The connection string approach (vs separate account name + key) is simpler
because `az storage` commands accept `--connection-string` directly.

---

## Data Protection considerations

The `DataProtectionKeys` SQL table is included in the backup and will be restored.
After restore, browser sessions that were created using keys generated after the backup
date will be invalidated — affected users will need to log in again. In dev this is
inconsequential. In prod, communicate to users before restoring.
