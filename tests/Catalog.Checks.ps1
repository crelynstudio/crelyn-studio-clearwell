$ErrorActionPreference = 'Stop'
$tokens=$null; $parseErrors=$null
$scriptPath=Join-Path $PSScriptRoot '..\tools\Check-Catalog.ps1'
$ast=[Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw ($parseErrors | Out-String) }
# Load only the classifier; tests must not query or modify WinGet sources.
$function=$ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-CatalogStatus' }, $false)
if ($null -eq $function) { throw 'Catalog status classifier not found.' }
. ([scriptblock]::Create($function.Extent.Text))
$cases=@(
    @{ Code=0; Expected='found' }
    @{ Code=-1978335162; Expected='source-agreements-required' }
    @{ Code=-1978335212; Expected='needs-review' }
    @{ Code=-1978335167; Expected='needs-review' }
    @{ Code=1; Expected='needs-review' }
    @{ Code=$null; Expected='timeout' }
)
foreach ($case in $cases) {
    $actual=Get-CatalogStatus $case.Code
    if ($actual -ne $case.Expected) { throw "Exit code $($case.Code): expected $($case.Expected), got $actual." }
}
Write-Output "Catalog status checks passed ($($cases.Count) cases)."
