param(
    [string]$RepoRoot = ".",
    [switch]$ApplyBrandingCleanup = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path $RepoRoot).Path
$sourceRoot = Join-Path $root "src/Muonroi.BuildingBlock"
$targetRoot = Join-Path $root "src"

if (-not (Test-Path $sourceRoot)) {
    throw "Source not found: $sourceRoot"
}

$maps = @(
    # Core
    @{ Package = "Muonroi.Core"; Source = "External/Common/Constants"; Target = "Constants"; NamespaceBase = "Muonroi.Core.Constants" },
    @{ Package = "Muonroi.Core"; Source = "External/Common/Enums"; Target = "Enums"; NamespaceBase = "Muonroi.Core.Enums" },
    @{ Package = "Muonroi.Core"; Source = "External/Common/Models"; Target = "Models/Common"; NamespaceBase = "Muonroi.Core.Models.Common" },
    @{ Package = "Muonroi.Core"; Source = "External/Configuration"; Target = "Configuration"; NamespaceBase = "Muonroi.Core.Configuration" },
    @{ Package = "Muonroi.Core"; Source = "External/Common/Pagination"; Target = "Pagination"; NamespaceBase = "Muonroi.Core.Pagination" },
    @{ Package = "Muonroi.Core"; Source = "External/Common/ValidationObject"; Target = "Validation"; NamespaceBase = "Muonroi.Core.Validation" },
    @{ Package = "Muonroi.Core"; Source = "External/Response"; Target = "Response"; NamespaceBase = "Muonroi.Core.Response" },
    @{ Package = "Muonroi.Core"; Source = "External/Timing"; Target = "Timing"; NamespaceBase = "Muonroi.Core.Timing" },
    @{ Package = "Muonroi.Core"; Source = "External/Models"; Target = "Models"; NamespaceBase = "Muonroi.Core.Models" },
    @{ Package = "Muonroi.Core"; Source = "External/Helper"; Target = "Helpers"; NamespaceBase = "Muonroi.Core.Helpers" },
    @{ Package = "Muonroi.Core"; Source = "External/Entity/MEntity.cs"; Target = "SeedWorks/Entity.cs"; NamespaceBase = "Muonroi.Core.SeedWorks"; Kind = "File" },

    # Core abstractions + data abstractions
    @{ Package = "Muonroi.Core.Abstractions"; Source = "External/Interfaces"; Target = "Interfaces"; NamespaceBase = "Muonroi.Core.Abstractions.Interfaces" },
    @{ Package = "Muonroi.Core.Abstractions"; Source = "Contract/Interfaces"; Target = "Contracts"; NamespaceBase = "Muonroi.Core.Abstractions.Contracts" },

    @{ Package = "Muonroi.Data.Abstractions"; Source = "External/UnitOfWork"; Target = "UnitOfWork"; NamespaceBase = "Muonroi.Data.Abstractions.UnitOfWork" },
    @{ Package = "Muonroi.Data.Abstractions"; Source = "External/Interfaces/IMRepository.cs"; Target = "Repositories/IRepository.cs"; NamespaceBase = "Muonroi.Data.Abstractions.Repositories"; Kind = "File" },
    @{ Package = "Muonroi.Data.Abstractions"; Source = "External/Interfaces/IMUnitOfWork.cs"; Target = "UnitOfWork/IUnitOfWork.cs"; NamespaceBase = "Muonroi.Data.Abstractions.UnitOfWork"; Kind = "File" },
    @{ Package = "Muonroi.Data.Abstractions"; Source = "External/Interfaces/IMQueries.cs"; Target = "Queries/IQuery.cs"; NamespaceBase = "Muonroi.Data.Abstractions.Queries"; Kind = "File" },

    # Data implementations
    @{ Package = "Muonroi.Data.EntityFrameworkCore"; Source = "External/Entity"; Target = "Entity"; NamespaceBase = "Muonroi.Data.EntityFrameworkCore.Entity" },
    @{ Package = "Muonroi.Data.EntityFrameworkCore"; Source = "External/Repositories"; Target = "Repositories"; NamespaceBase = "Muonroi.Data.EntityFrameworkCore.Repositories" },
    @{ Package = "Muonroi.Data.EntityFrameworkCore"; Source = "External/Extensions/DbContextExtensions.cs"; Target = "Extensions/DbContextExtensions.cs"; NamespaceBase = "Muonroi.Data.EntityFrameworkCore.Extensions"; Kind = "File" },

    @{ Package = "Muonroi.Data.Dapper"; Source = "External/ORMs/Dapper"; Target = "Dapper"; NamespaceBase = "Muonroi.Data.Dapper" },

    # Caching
    @{ Package = "Muonroi.Caching.Abstractions"; Source = "External/Caching/Distributed/DistributedCacheKeyBuilder.cs"; Target = "Distributed/DistributedCacheKeyBuilder.cs"; NamespaceBase = "Muonroi.Caching.Abstractions.Distributed"; Kind = "File" },
    @{ Package = "Muonroi.Caching.Abstractions"; Source = "External/Caching/Distributed/DistributedCacheRuntimeTelemetry.cs"; Target = "Distributed/DistributedCacheRuntimeTelemetry.cs"; NamespaceBase = "Muonroi.Caching.Abstractions.Distributed"; Kind = "File" },
    @{ Package = "Muonroi.Caching.Memory"; Source = "External/Caching/Distributed/MultiLevel"; Target = "MultiLevel"; NamespaceBase = "Muonroi.Caching.Memory.MultiLevel" },
    @{ Package = "Muonroi.Caching.Redis"; Source = "External/Caching/Distributed/Redis"; Target = "Redis"; NamespaceBase = "Muonroi.Caching.Redis" },

    # Messaging + mediator
    @{ Package = "Muonroi.Messaging.Abstractions"; Source = "External/Events"; Target = "Events"; NamespaceBase = "Muonroi.Messaging.Abstractions.Events" },
    @{ Package = "Muonroi.Messaging.Abstractions"; Source = "External/InternalEvents"; Target = "InternalEvents"; NamespaceBase = "Muonroi.Messaging.Abstractions.InternalEvents" },
    @{ Package = "Muonroi.Messaging.MassTransit"; Source = "External/Messaging"; Target = "Messaging"; NamespaceBase = "Muonroi.Messaging.MassTransit" },
    @{ Package = "Muonroi.Mediator"; Source = "External/Mediator"; Target = "Mediator"; NamespaceBase = "Muonroi.Mediator" },
    @{ Package = "Muonroi.Mediator"; Source = "Internal/Behaviours"; Target = "Behaviours"; NamespaceBase = "Muonroi.Mediator.Behaviours" },
    @{ Package = "Muonroi.Auth"; Source = "External/BearerToken"; Target = "BearerToken"; NamespaceBase = "Muonroi.Auth.BearerToken" },
    @{ Package = "Muonroi.Auth"; Source = "External/User"; Target = "User"; NamespaceBase = "Muonroi.Auth.User" },

    # Web + communication
    @{ Package = "Muonroi.AspNetCore"; Source = "External/Controller"; Target = "Controllers"; NamespaceBase = "Muonroi.AspNetCore.Controllers" },
    @{ Package = "Muonroi.AspNetCore"; Source = "External/Middleware"; Target = "Middleware"; NamespaceBase = "Muonroi.AspNetCore.Middleware" },
    @{ Package = "Muonroi.AspNetCore"; Source = "External/Filters"; Target = "Filters"; NamespaceBase = "Muonroi.AspNetCore.Filters" },
    @{ Package = "Muonroi.AspNetCore"; Source = "External/Cors"; Target = "Cors"; NamespaceBase = "Muonroi.AspNetCore.Cors" },
    @{ Package = "Muonroi.AspNetCore"; Source = "External/Attributes"; Target = "Attributes"; NamespaceBase = "Muonroi.AspNetCore.Attributes" },

    @{ Package = "Muonroi.AspNetCore.OpenApi"; Source = "External/Default"; Target = "OpenApi"; NamespaceBase = "Muonroi.AspNetCore.OpenApi" },
    @{ Package = "Muonroi.Grpc"; Source = "External/Grpc"; Target = "Grpc"; NamespaceBase = "Muonroi.Grpc" },
    @{ Package = "Muonroi.Grpc"; Source = "External/ExternalApp/BaseGrpcService.cs"; Target = "Services/BaseGrpcService.cs"; NamespaceBase = "Muonroi.Grpc.Services"; Kind = "File" },
    @{ Package = "Muonroi.SignalR"; Source = "External/SignalR"; Target = "SignalR"; NamespaceBase = "Muonroi.SignalR" },

    # Infrastructure
    @{ Package = "Muonroi.Observability"; Source = "External/Observability"; Target = "OpenTelemetry"; NamespaceBase = "Muonroi.Observability.OpenTelemetry" },
    @{ Package = "Muonroi.Observability"; Source = "External/Logging"; Target = "Logging"; NamespaceBase = "Muonroi.Observability.Logging" },

    @{ Package = "Muonroi.BackgroundJobs.Abstractions"; Source = "External/BackgroundJobs/JobType.cs"; Target = "JobType.cs"; NamespaceBase = "Muonroi.BackgroundJobs.Abstractions"; Kind = "File" },
    @{ Package = "Muonroi.BackgroundJobs.Abstractions"; Source = "External/BackgroundJobs/TenantAwareJobBase.cs"; Target = "TenantAwareJobBase.cs"; NamespaceBase = "Muonroi.BackgroundJobs.Abstractions"; Kind = "File" },
    @{ Package = "Muonroi.BackgroundJobs.Hangfire"; Source = "External/BackgroundJobs/BackgroundJobConfigs.cs"; Target = "Hangfire/HangfireJobOptions.cs"; NamespaceBase = "Muonroi.BackgroundJobs.Hangfire"; Kind = "File" },
    @{ Package = "Muonroi.BackgroundJobs.Hangfire"; Source = "External/BackgroundJobs/BackgroundJobHandler.cs"; Target = "Hangfire/HangfireJobScheduler.cs"; NamespaceBase = "Muonroi.BackgroundJobs.Hangfire"; Kind = "File" },
    @{ Package = "Muonroi.BackgroundJobs.Quartz"; Source = "External/BackgroundJobs/BackgroundJobConfigs.cs"; Target = "Quartz/QuartzJobOptions.cs"; NamespaceBase = "Muonroi.BackgroundJobs.Quartz"; Kind = "File" },
    @{ Package = "Muonroi.BackgroundJobs.Quartz"; Source = "External/BackgroundJobs/BackgroundJobHandler.cs"; Target = "Quartz/QuartzJobScheduler.cs"; NamespaceBase = "Muonroi.BackgroundJobs.Quartz"; Kind = "File" },

    @{ Package = "Muonroi.ServiceDiscovery.Consul"; Source = "External/Consul"; Target = "Consul"; NamespaceBase = "Muonroi.ServiceDiscovery.Consul" },
    @{ Package = "Muonroi.Kubernetes"; Source = "External/Kubernetes"; Target = "Kubernetes"; NamespaceBase = "Muonroi.Kubernetes" },
    @{ Package = "Muonroi.Resilience"; Source = "External/Polly"; Target = "Policies"; NamespaceBase = "Muonroi.Resilience.Policies" },

    # Governance + runtime
    @{ Package = "Muonroi.Governance"; Source = "Shared/Policy"; Target = "Policy"; NamespaceBase = "Muonroi.Governance.Policy" },
    @{ Package = "Muonroi.Governance"; Source = "Shared/Authorization"; Target = "Authorization"; NamespaceBase = "Muonroi.Governance.Authorization" },
    @{ Package = "Muonroi.Governance"; Source = "Shared/License"; Target = "License"; NamespaceBase = "Muonroi.Governance.License" },
    @{ Package = "Muonroi.Governance"; Source = "Shared/ControlPlane"; Target = "ControlPlane"; NamespaceBase = "Muonroi.Governance.ControlPlane" },
    @{ Package = "Muonroi.Governance"; Source = "Shared/ServerValidation"; Target = "ServerValidation"; NamespaceBase = "Muonroi.Governance.ServerValidation" },
    @{ Package = "Muonroi.Governance"; Source = "Shared/Compliance"; Target = "Compliance"; NamespaceBase = "Muonroi.Governance.Compliance" },
    @{ Package = "Muonroi.Governance"; Source = "Shared/Operations"; Target = "Operations"; NamespaceBase = "Muonroi.Governance.Operations" },
    @{ Package = "Muonroi.RuleEngine.Runtime"; Source = "Shared/Rules"; Target = "Rules"; NamespaceBase = "Muonroi.RuleEngine.Runtime.Rules" },

    # Tenancy extraction continuation
    @{ Package = "Muonroi.Tenancy.Abstractions"; Source = "External/Tenant/ITenantContext.cs"; Target = "Legacy/ITenantContext.cs"; NamespaceBase = "Muonroi.Tenancy.Abstractions.Legacy"; Kind = "File" },
    @{ Package = "Muonroi.Tenancy.Abstractions"; Source = "External/Tenant/ITenantIdResolver.cs"; Target = "Legacy/ITenantIdResolver.cs"; NamespaceBase = "Muonroi.Tenancy.Abstractions.Legacy"; Kind = "File" },
    @{ Package = "Muonroi.Tenancy.Core"; Source = "External/Tenant"; Target = "Legacy"; NamespaceBase = "Muonroi.Tenancy.Core.Legacy" },
    @{ Package = "Muonroi.Tenancy.Core"; Source = "Shared/Tenancy"; Target = "Shared"; NamespaceBase = "Muonroi.Tenancy.Core.Shared" }
)

$globalNamespaceReplacements = [ordered]@{
    "Muonroi.BuildingBlock.External.Common.Constants" = "Muonroi.Core.Constants";
    "Muonroi.BuildingBlock.External.Common.Enums" = "Muonroi.Core.Enums";
    "Muonroi.BuildingBlock.External.Common.Models.Requests.Login" = "Muonroi.Core.Models.Common.Requests.Login";
    "Muonroi.BuildingBlock.External.Common.Models.Requests.Registers" = "Muonroi.Core.Models.Common.Requests.Registers";
    "Muonroi.BuildingBlock.External.Common.Models.Responses.Login" = "Muonroi.Core.Models.Common.Responses.Login";
    "Muonroi.BuildingBlock.External.Common.Models" = "Muonroi.Core.Models.Common";
    "Muonroi.BuildingBlock.External.Common.Pagination" = "Muonroi.Core.Pagination";
    "Muonroi.BuildingBlock.External.Common.ValidationObject" = "Muonroi.Core.Validation";
    "Muonroi.BuildingBlock.External.Response" = "Muonroi.Core.Response";
    "Muonroi.BuildingBlock.External.Timing" = "Muonroi.Core.Timing";
    "Muonroi.BuildingBlock.External.Configuration" = "Muonroi.Core.Configuration";
    "Muonroi.BuildingBlock.External.SeedWorks" = "Muonroi.Core.SeedWorks";
    "Muonroi.BuildingBlock.External.Interfaces" = "Muonroi.Core.Abstractions.Interfaces";
    "Muonroi.BuildingBlock.External.UnitOfWork" = "Muonroi.Data.Abstractions.UnitOfWork";
    "Muonroi.BuildingBlock.External.ORMs.Dapper" = "Muonroi.Data.Dapper";
    "Muonroi.BuildingBlock.External.Entity" = "Muonroi.Data.EntityFrameworkCore.Entity";
    "Muonroi.BuildingBlock.External.BearerToken.Signers" = "Muonroi.Auth.BearerToken.Signers";
    "Muonroi.BuildingBlock.External.BearerToken" = "Muonroi.Auth.BearerToken";
    "Muonroi.BuildingBlock.External.User" = "Muonroi.Auth.User";
    "Muonroi.BuildingBlock.External.Caching.Distributed.Redis" = "Muonroi.Caching.Redis";
    "Muonroi.BuildingBlock.External.Caching.Distributed.MultiLevel" = "Muonroi.Caching.Memory.MultiLevel";
    "Muonroi.BuildingBlock.External.Caching.Distributed" = "Muonroi.Caching.Abstractions.Distributed";
    "Muonroi.BuildingBlock.External.Events" = "Muonroi.Messaging.Abstractions.Events";
    "Muonroi.BuildingBlock.External.InternalEvents" = "Muonroi.Messaging.Abstractions.InternalEvents";
    "Muonroi.BuildingBlock.External.Messaging" = "Muonroi.Messaging.MassTransit";
    "Muonroi.BuildingBlock.External.Mediator" = "Muonroi.Mediator";
    "Muonroi.BuildingBlock.External.Controller" = "Muonroi.AspNetCore.Controllers";
    "Muonroi.BuildingBlock.External.Middleware" = "Muonroi.AspNetCore.Middleware";
    "Muonroi.BuildingBlock.External.Filters" = "Muonroi.AspNetCore.Filters";
    "Muonroi.BuildingBlock.External.Grpc" = "Muonroi.Grpc";
    "Muonroi.BuildingBlock.External.SignalR" = "Muonroi.SignalR";
    "Muonroi.BuildingBlock.External.Observability" = "Muonroi.Observability.OpenTelemetry";
    "Muonroi.BuildingBlock.External.Logging" = "Muonroi.Observability.Logging";
    "Muonroi.BuildingBlock.External.Consul" = "Muonroi.ServiceDiscovery.Consul";
    "Muonroi.BuildingBlock.External.Kubernetes" = "Muonroi.Kubernetes";
    "Muonroi.BuildingBlock.External.Polly" = "Muonroi.Resilience.Policies";
    "Muonroi.BuildingBlock.Shared.Authorization" = "Muonroi.Governance.Authorization";
    "Muonroi.BuildingBlock.Shared.Policy" = "Muonroi.Governance.Policy";
    "Muonroi.BuildingBlock.Shared.ControlPlane" = "Muonroi.Governance.ControlPlane";
    "Muonroi.BuildingBlock.Shared.ServerValidation" = "Muonroi.Governance.ServerValidation";
    "Muonroi.BuildingBlock.Shared.License" = "Muonroi.Governance.License";
    "Muonroi.BuildingBlock.Shared.Compliance" = "Muonroi.Governance.Compliance";
    "Muonroi.BuildingBlock.Shared.Operations" = "Muonroi.Governance.Operations";
    "Muonroi.BuildingBlock.Shared.Tenancy" = "Muonroi.Tenancy.Core.Shared";
    "Muonroi.BuildingBlock.Shared.Rules" = "Muonroi.RuleEngine.Runtime.Rules";
    "Muonroi.BuildingBlock.External.Tenant" = "Muonroi.Tenancy.Core.Legacy"
}

$brandingReplacements = [ordered]@{
    # Keep M* brand prefix, but standardize to explicit domain prefixes.
    "MAuthInfoContext" = "MAuthContext";
    "InteralAuInfoContext" = "MAuthContext";
    "MGenericController" = "MWebGenericController";
    "MAuthControllerBase" = "MWebAuthControllerBase";
    "MControllerBase" = "MWebControllerBase";
    "MEFUnitOfWork" = "MDataEfUnitOfWork";
    "MEFRepository" = "MDataEfRepository";
    "MDapperRepository" = "MDataDapperRepository";
    "MRedisCacheService" = "MCacheRedisService";
    "MDateTimeExtension" = "MCoreDateTimeExtensions";
    "MStringExtension" = "MCoreStringExtensions"
}

function Get-SourceNamespaceRoot {
    param([hashtable]$Map)
    $source = $Map.Source
    $kind = if ($Map.ContainsKey("Kind")) { [string]$Map.Kind } else { "Dir" }
    if ($kind -eq "File") {
        $source = Split-Path $source -Parent
    }
    return ("Muonroi.BuildingBlock." + ($source -replace "[\\/]", ".").Trim("."))
}

function Apply-ContentTransforms {
    param(
        [string]$Content,
        [string]$NamespaceValue,
        [string]$SourceNamespaceRoot,
        [string]$TargetNamespaceRoot
    )

    $updated = $Content

    $namespacePattern = '(?m)^(\s*namespace\s+)[A-Za-z0-9_.]+(\s*;?)$'
    if ([regex]::IsMatch($updated, $namespacePattern)) {
        $updated = [regex]::Replace($updated, $namespacePattern, "`${1}$NamespaceValue`${2}", 1)
    }

    if ($SourceNamespaceRoot -and $TargetNamespaceRoot) {
        $updated = $updated.Replace($SourceNamespaceRoot, $TargetNamespaceRoot)
    }

    foreach ($k in $globalNamespaceReplacements.Keys) {
        $updated = $updated.Replace($k, $globalNamespaceReplacements[$k])
    }

    if ($ApplyBrandingCleanup) {
        foreach ($k in $brandingReplacements.Keys) {
            $updated = $updated.Replace($k, $brandingReplacements[$k])
        }
    }

    return $updated
}

function Copy-MappedEntry {
    param([hashtable]$Map)

    $packageDir = Join-Path $targetRoot $Map.Package
    if (-not (Test-Path $packageDir)) {
        Write-Warning "Skip missing package: $($Map.Package)"
        return 0
    }

    $targetBase = Join-Path $packageDir $Map.Target
    $kind = if ($Map.ContainsKey("Kind")) { [string]$Map.Kind } else { "Dir" }
    $sourcePath = Join-Path $sourceRoot $Map.Source
    $sourceNamespaceRoot = Get-SourceNamespaceRoot -Map $Map
    $targetNamespaceRoot = $Map.NamespaceBase
    $copied = 0

    if ($kind -eq "File") {
        if (-not (Test-Path $sourcePath)) {
            Write-Warning "Missing source file: $sourcePath"
            return 0
        }

        $destFile = $targetBase
        $destDir = Split-Path $destFile -Parent
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }

        $ext = [System.IO.Path]::GetExtension($sourcePath)
        if ($ext -ieq ".cs") {
            $content = Get-Content -Path $sourcePath -Raw
            $transformed = Apply-ContentTransforms -Content $content -NamespaceValue $Map.NamespaceBase -SourceNamespaceRoot $sourceNamespaceRoot -TargetNamespaceRoot $targetNamespaceRoot
            Set-Content -Path $destFile -Value $transformed -NoNewline
        } else {
            Copy-Item -Path $sourcePath -Destination $destFile -Force
        }

        return 1
    }

    if (-not (Test-Path $sourcePath)) {
        Write-Warning "Missing source dir: $sourcePath"
        return 0
    }

    $files = Get-ChildItem -Path $sourcePath -Recurse -File |
        Where-Object { $_.Extension -in @(".cs", ".json", ".md", ".txt") }

    foreach ($file in $files) {
        $rel = $file.FullName.Substring($sourcePath.Length).TrimStart("\", "/")
        $destFile = if ([string]::IsNullOrWhiteSpace($Map.Target)) {
            Join-Path $packageDir $rel
        } else {
            Join-Path $targetBase $rel
        }
        $destDir = Split-Path $destFile -Parent
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }

        if ($file.Extension -ieq ".cs") {
            $content = Get-Content -Path $file.FullName -Raw
            $subDir = Split-Path $rel -Parent
            $namespaceValue = $Map.NamespaceBase
            if ($subDir -and $subDir -ne ".") {
                $suffix = ($subDir -replace "[\\/]", ".").Trim(".")
                if ($suffix) {
                    $namespaceValue = "$($Map.NamespaceBase).$suffix"
                }
            }
            $transformed = Apply-ContentTransforms -Content $content -NamespaceValue $namespaceValue -SourceNamespaceRoot $sourceNamespaceRoot -TargetNamespaceRoot $targetNamespaceRoot
            Set-Content -Path $destFile -Value $transformed -NoNewline
        } else {
            Copy-Item -Path $file.FullName -Destination $destFile -Force
        }

        $copied++
    }

    return $copied
}

$placeholderFiles = Get-ChildItem -Path $targetRoot -Recurse -File -Filter "Class1.cs" |
    Where-Object { $_.FullName -notlike "*\Muonroi.BuildingBlock\*" }
foreach ($placeholder in $placeholderFiles) {
    Remove-Item -Path $placeholder.FullName -Force
}

$totalCopied = 0
foreach ($map in $maps) {
    $count = Copy-MappedEntry -Map $map
    $totalCopied += $count
    Write-Host ("{0,-36} | {1,-35} -> {2,-30} | +{3}" -f $map.Package, $map.Source, $map.Target, $count)
}

Write-Host ""
Write-Host "Total files copied/transformed: $totalCopied"
