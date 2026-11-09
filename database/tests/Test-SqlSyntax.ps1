[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $ScriptDomPath
)

$ErrorActionPreference = 'Stop'
# ScriptDom ships with Visual Studio SQL tooling and SQL Server Management Studio.
Add-Type -Path $ScriptDomPath
$parser = [Microsoft.SqlServer.TransactSql.ScriptDom.TSql160Parser]::new($true)
$failureCount = 0
$databasePath = Split-Path -Parent $PSScriptRoot
foreach ($file in Get-ChildItem -LiteralPath $databasePath -Filter '*.sql' -Recurse) {
    $reader = [System.IO.StringReader]::new([System.IO.File]::ReadAllText($file.FullName))
    try {
        $parseErrors = $null
        $null = $parser.Parse($reader, [ref] $parseErrors)
        if ($parseErrors.Count -gt 0) {
            $failureCount += $parseErrors.Count
            foreach ($parseError in $parseErrors) {
                Write-Output "$($file.Name):$($parseError.Line):$($parseError.Column): $($parseError.Message)"
            }
        } else {
            Write-Output "PASS: $($file.Name)"
        }
    } finally {
        $reader.Dispose()
    }
}
if ($failureCount -gt 0) { throw "SQL syntax validation failed with $failureCount errors." }
Write-Output 'Syntax validation passed. Database execution and concurrency tests are separate checks.'
