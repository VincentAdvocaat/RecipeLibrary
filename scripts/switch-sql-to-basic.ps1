param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$rg = 'rg-recipelibrary-test-sec'
$server = 'sql-recipelibrary-test-sec-gdqfvo7drqwie'
$database = 'recipelibrary-test-sec'
$subscription = [string](az account show --query id -o tsv)
if ($LASTEXITCODE -ne 0 -or $subscription.Trim() -ne '89578d9e-47b2-41f4-b12c-c66e216e2215') { throw 'Unexpected subscription' }

function Read-Database {
    $result = az sql db show -g $rg -s $server -n $database -o json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'SQL management read failed' }
    return $result
}

$current = Read-Database
if ($current.sku.tier -eq 'Basic') {
    if ($current.status -ne 'Online' -or $current.sku.capacity -ne 5 -or $current.maxSizeBytes -gt 2147483648 -or $current.useFreeLimit) { throw 'Unexpected Basic configuration' }
    Write-Output 'Already Basic 5 DTU / <=2 GiB; no update performed.'
    return
}
if ($current.sku.name -ne 'GP_S_Gen5' -or $current.sku.capacity -ne 2) { throw 'Unexpected source SQL plan' }

# Management metrics only: do not connect to/resume the exhausted free database for sizing.
$start = [datetime]::UtcNow.AddDays(-2).ToString('yyyy-MM-ddTHH:mm:ssZ')
$end = [datetime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
$metrics = az monitor metrics list --resource $current.id --metric allocated_data_storage --start-time $start --end-time $end --interval PT1H --aggregation Maximum -o json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'SQL size metrics unavailable' }
$points = @($metrics.value.timeseries.data | Where-Object { $null -ne $_.maximum })
if ($points.Count -eq 0) { throw 'No recent allocation evidence; conversion stopped' }
$latest = $points | Sort-Object timeStamp | Select-Object -Last 1
$largest = ($points | Measure-Object maximum -Maximum).Maximum
if (([datetimeoffset]::UtcNow - [datetimeoffset]$latest.timeStamp).TotalHours -gt 36 -or $largest -gt 1610612736) {
    throw 'Allocation evidence is stale or lacks safe headroom below 2 GiB'
}
[pscustomobject]@{mode = $(if ($Apply) {'apply'} else {'dry-run'}); allocatedBytes = $largest; metricAt = $latest.timeStamp; target = 'Basic / 5 DTU / 2 GiB'; permanentFreeOfferRemoval = $true} | ConvertTo-Json
if (!$Apply) { return }

# The user's approved move to Basic permanently leaves the free offer. Keep the
# same server/database: Azure changes the service tier without dropping data/users.
if ($current.useFreeLimit) {
    az sql db update -g $rg -s $server -n $database --use-free-limit false --output none
    if ($LASTEXITCODE -ne 0) { throw 'Free-offer removal failed; inspect SQL state before further action' }
    if ((Read-Database).useFreeLimit) { throw 'Free offer still enabled; conversion stopped' }
}
az sql db update -g $rg -s $server -n $database --edition Basic --service-objective Basic --max-size 2GB --output none
if ($LASTEXITCODE -ne 0) { throw 'Basic conversion failed after free-offer removal; paid General Purpose may remain. Inspect and repair this database; do not create a replacement.' }
$result = Read-Database
if ($result.status -ne 'Online' -or $result.sku.tier -ne 'Basic' -or $result.sku.capacity -ne 5 -or $result.maxSizeBytes -ne 2147483648 -or $result.useFreeLimit) { throw 'Basic conversion verification failed' }
Write-Output 'Verified Basic 5 DTU / 2 GiB; existing database identity retained.'
