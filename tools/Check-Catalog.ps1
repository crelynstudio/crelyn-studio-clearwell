param([string]$Output = (Join-Path $PSScriptRoot '..\artifacts\catalog-health.csv'), [string[]]$PackageId = @())
$ErrorActionPreference = 'Stop'
function Get-CatalogStatus([Nullable[int]]$ExitCode) {
    if ($null -eq $ExitCode) { return 'timeout' }
    if ($ExitCode -eq 0) { return 'found' }
    # Source agreements remain unaccepted during this read-only check (0x8A150046).
    if ($ExitCode -eq -1978335162) { return 'source-agreements-required' }
    return 'needs-review'
}
$winget = (Get-Command winget.exe -ErrorAction Stop).Source
$catalog = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\src\Clearwell\Core\WindowsTools.cs') -Encoding UTF8
$entries = foreach ($line in $catalog) {
    if ($line -match '^\s*new\("(?<id>[A-Za-z0-9._+-]+)",') {
        $id = $Matches.id
        if ($PackageId.Count -gt 0 -and $id -notin $PackageId) { continue }
        [pscustomobject]@{ Id=$id; Source=$(if ($line -match ', "msstore"\),?$') { 'msstore' } else { 'winget' }) }
    }
}
if (@($entries).Count -eq 0) { throw 'No catalog entries matched.' }
$results = foreach ($entry in $entries) {
    $start = [Diagnostics.ProcessStartInfo]::new($winget)
    $start.UseShellExecute=$false; $start.CreateNoWindow=$true; $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    foreach ($argument in @('show','--id',$entry.Id,'--exact','--source',$entry.Source,'--disable-interactivity')) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
    if ($process.WaitForExit(30000)) { $code=$process.ExitCode }
    else { $process.Kill($true); $code=$null }
    $state=Get-CatalogStatus $code
    $process.Dispose()
    [pscustomobject]@{ Id=$entry.Id; Source=$entry.Source; Status=$state; ExitCode=$code; CheckedUtc=[DateTime]::UtcNow.ToString('O') }
}
$Output=[IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path (Split-Path -Parent $Output) -Force | Out-Null
$results | Export-Csv -LiteralPath $Output -NoTypeInformation -Encoding utf8
$results | Group-Object Status | Select-Object Name,Count | Format-Table
Write-Output "Read-only catalog health report: $Output"
$results | Where-Object Status -ne 'found' | Select-Object Id,Source,Status,ExitCode | Format-Table
foreach ($result in $results | Where-Object Status -eq 'source-agreements-required') {
    $message="$($result.Id) ($($result.Source)): not verified because source agreements are not accepted. No agreements were accepted by this check."
    if ($env:GITHUB_ACTIONS -eq 'true') { Write-Output "::warning::$message" }
    else { Write-Warning $message }
}
if (@($results | Where-Object Status -in @('needs-review','timeout')).Count -gt 0) { throw 'Some entries need review. This can indicate source/network/region issues, not necessarily invalid package IDs.' }
