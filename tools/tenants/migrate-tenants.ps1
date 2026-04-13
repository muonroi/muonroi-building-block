# Requires Az.Sql module
param(
    [Parameter(Mandatory)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory)]
    [string]$ServerName,

    # Elastic job agent database name
    [Parameter(Mandatory)]
    [string]$AgentDatabase,

    # SQL credential stored in the agent database
    [Parameter(Mandatory)]
    [string]$CredentialName,

    # Path to migration SQL script executed for each tenant database
    [Parameter(Mandatory)]
    [string]$ScriptPath,

    # List of tenant database names to migrate
    [Parameter(Mandatory)]
    [string[]]$TenantDatabases,

    [switch]$WhatIf
)

Import-Module Az.Sql -ErrorAction Stop

$agentName = $AgentDatabase
$jobName = 'tenant-migration'
$targetGroupName = 'tenant-databases'

# Create target group containing tenant databases
if ($WhatIf) {
    Write-Host "Would create target group $targetGroupName with databases: $TenantDatabases" -ForegroundColor Yellow
} else {
    if (-not(Get-AzSqlElasticJobTargetGroup -ResourceGroupName $ResourceGroupName -ServerName $ServerName -AgentName $agentName -Name $targetGroupName -ErrorAction SilentlyContinue)) {
        New-AzSqlElasticJobTargetGroup -ResourceGroupName $ResourceGroupName -ServerName $ServerName -AgentName $agentName -Name $targetGroupName | Out-Null
    }
    foreach ($db in $TenantDatabases) {
        Add-AzSqlElasticJobTarget -ResourceGroupName $ResourceGroupName -ServerName $ServerName -AgentName $agentName -TargetGroupName $targetGroupName -ServerName $ServerName -DatabaseName $db -RefreshCredentialName $CredentialName | Out-Null
    }
}

# Create job
if ($WhatIf) {
    Write-Host "Would create elastic job $jobName" -ForegroundColor Yellow
} else {
    if (-not(Get-AzSqlElasticJob -ResourceGroupName $ResourceGroupName -ServerName $ServerName -AgentName $agentName -Name $jobName -ErrorAction SilentlyContinue)) {
        New-AzSqlElasticJob -ResourceGroupName $ResourceGroupName -ServerName $ServerName -AgentName $agentName -Name $jobName | Out-Null
    }
}

# Create job step executing migration script
$commandText = Get-Content $ScriptPath -Raw

if ($WhatIf) {
    Write-Host "Would add job step to run $ScriptPath" -ForegroundColor Yellow
} else {
    if (-not(Get-AzSqlElasticJobStep -ResourceGroupName $ResourceGroupName -ServerName $ServerName -AgentName $agentName -JobName $jobName -Name 'migrate' -ErrorAction SilentlyContinue)) {
        New-AzSqlElasticJobStep -ResourceGroupName $ResourceGroupName -ServerName $ServerName -AgentName $agentName -JobName $jobName -Name 'migrate' -TargetGroupName $targetGroupName -CredentialName $CredentialName -CommandText $commandText | Out-Null
    } else {
        Update-AzSqlElasticJobStep -ResourceGroupName $ResourceGroupName -ServerName $ServerName -AgentName $agentName -JobName $jobName -Name 'migrate' -TargetGroupName $targetGroupName -CredentialName $CredentialName -CommandText $commandText | Out-Null
    }
    Start-AzSqlElasticJob -ResourceGroupName $ResourceGroupName -ServerName $ServerName -AgentName $agentName -Name $jobName | Out-Null
    Write-Host "Started migration job $jobName" -ForegroundColor Green
}
