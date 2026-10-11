param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts\releases'), [string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$version = ([xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+-preview\.\d+$') { throw 'This pipeline produces preview packages only.' }
$name = "Clearwell-$version-win-x64"
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$package = Join-Path $OutputDirectory $name
$zip = "$package.zip"
foreach ($target in @($package, $zip, "$zip.sha256", "$package.provenance.json")) {
    if (Test-Path -LiteralPath $target) { throw "Refusing to overwrite existing release: $target" }
}
$work = Join-Path $PSScriptRoot ('artifacts\release-validation-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
$destination = $package
$package = Join-Path $work $name
$zip = "$package.zip"
$dotnetCommand = (Get-Command $Dotnet -ErrorAction Stop).Source
$env:DOTNET_ROOT = Split-Path -Parent $dotnetCommand
& $Dotnet restore (Join-Path $PSScriptRoot 'Clearwell.slnx') --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
& $Dotnet build (Join-Path $PSScriptRoot 'Clearwell.slnx') -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$checkOutput = & $Dotnet run --project (Join-Path $PSScriptRoot 'tests\Clearwell.Checks') -c Release --no-build -- (Join-Path $work 'checks') 2>&1
$checkOutput | Out-File -LiteralPath (Join-Path $work 'checks.txt') -Encoding utf8
if ($LASTEXITCODE -ne 0) { $checkOutput | Write-Output; throw 'Checks failed.' }
$checkOutput | Select-Object -Last 1 | Write-Output
& (Join-Path $PSScriptRoot 'publish.ps1') -OutputDirectory $package -Dotnet $Dotnet
Compress-Archive -LiteralPath $package -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$zip.sha256", "$hash  $name.zip`n")
$extract = Join-Path $work 'independent-package'
Expand-Archive -LiteralPath $zip -DestinationPath $extract
$independent = Join-Path $extract $name
foreach ($file in Get-ChildItem -LiteralPath $package -File) {
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $independent $file.Name)).Hash) { throw "Archive mismatch: $($file.Name)" }
}
if ((Get-FileHash -LiteralPath $zip).Hash.ToLowerInvariant() -ne $hash) { throw 'ZIP checksum mismatch.' }
$exe = Join-Path $independent 'Clearwell.exe'
$binaryVersion = (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion.Split('+')[0]
if ($binaryVersion -ne $version) { throw "Binary version mismatch: $binaryVersion" }
$ui = Join-Path $work 'ui'
$process = Start-Process -FilePath $exe -ArgumentList @('--verify-ui', ('"' + $ui + '"')) -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(120000)) { $process.Kill(); throw 'UI verification exceeded two minutes.' }
$result = Get-Content -LiteralPath (Join-Path $ui 'result.txt') -Raw
if ($process.ExitCode -ne 0 -or $result -notlike 'PASS:*') { throw "Portable UI verification failed: $result" }
$commit = $null; $dirty = $null
if ((Get-Command git -ErrorAction SilentlyContinue) -and (Test-Path -LiteralPath (Join-Path $PSScriptRoot '.git'))) {
    $commit = & git -C $PSScriptRoot rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source commit.' }
    $dirty = @(& git -C $PSScriptRoot status --porcelain).Count -gt 0
}
$provenance = [ordered]@{ version=$version; runtime='win-x64'; sourceCommit=$commit; sourceHasChanges=$dirty; sha256=$hash; binarySha256=(Get-FileHash -LiteralPath $exe).Hash.ToLowerInvariant(); signed=$false; checks='passed'; portableUi='passed'; createdUtc=[DateTime]::UtcNow.ToString('O') }
$provenance | ConvertTo-Json | Out-File -LiteralPath "$package.provenance.json" -Encoding utf8
if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($destination)) -ne $OutputDirectory -or -not [IO.Path]::GetFullPath($package).StartsWith([IO.Path]::GetFullPath($work) + [IO.Path]::DirectorySeparatorChar)) { throw 'Release paths escaped their expected directories.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Move-Item -LiteralPath $package -Destination $destination
Move-Item -LiteralPath $zip -Destination "$destination.zip"
Move-Item -LiteralPath "$zip.sha256" -Destination "$destination.zip.sha256"
Move-Item -LiteralPath "$package.provenance.json" -Destination "$destination.provenance.json"
Write-Output "Verified preview: $destination.zip"
Write-Output "SHA-256: $hash"
Write-Output "Local validation evidence: $work"
