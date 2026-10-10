param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts\Clearwell-0.2.0-preview.1-win-x64'),
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$project = Join-Path $PSScriptRoot 'src\Clearwell\Clearwell.csproj'
$publishLock = Join-Path $PSScriptRoot 'src\Clearwell\packages.publish.lock.json'
& $Dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false "-p:NuGetLockFilePath=$publishLock" -o $OutputDirectory -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Publish failed / Derleme başarısız.' }
$assets = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src\Clearwell\obj\project.assets.json') -Raw | ConvertFrom-Json
$cache = $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src\Clearwell\bin\Release\net10.0-windows\win-x64\Clearwell.deps.json') -Raw | ConvertFrom-Json
$runtime = ($manifest.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'runtimepack.Microsoft.NETCore.App.Runtime.win-x64/*' } | Select-Object -First 1) -replace '^runtimepack\.', ''
$desktop = ($manifest.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64/*' } | Select-Object -First 1) -replace '^runtimepack\.', ''
if (-not $runtime -or -not $desktop) { throw 'Runtime license locations could not be resolved.' }
Copy-Item -LiteralPath (Join-Path $cache ($runtime.ToLowerInvariant() + '/LICENSE.TXT')) -Destination (Join-Path $OutputDirectory 'DOTNET-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $cache ($runtime.ToLowerInvariant() + '/THIRD-PARTY-NOTICES.TXT')) -Destination (Join-Path $OutputDirectory 'DOTNET-THIRD-PARTY-NOTICES.txt')
Copy-Item -LiteralPath (Join-Path $cache ($desktop.ToLowerInvariant() + '/LICENSE')) -Destination (Join-Path $OutputDirectory 'WINDOWS-DESKTOP-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE'),(Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.txt') -Destination $OutputDirectory
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\PORTABLE-README.txt') -Destination (Join-Path $OutputDirectory 'README.txt')
Write-Output "Development preview / Geliştirme önizlemesi: $OutputDirectory"
