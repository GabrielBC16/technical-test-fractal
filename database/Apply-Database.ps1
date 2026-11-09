[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $Server,
    [Parameter(Mandatory = $true)] [string] $Database,
    [switch] $UseIntegratedSecurity,
    [switch] $TrustServerCertificate,
    [switch] $SkipSecurity
)

$ErrorActionPreference = 'Stop'
$sqlcmd = Get-Command sqlcmd -ErrorAction Stop
$arguments = @('-S', $Server, '-d', $Database, '-b', '-r', '1')
if ($TrustServerCertificate) { $arguments += '-C' }
if ($UseIntegratedSecurity) {
    $arguments += '-E'
} else {
    if ([string]::IsNullOrWhiteSpace($env:SQLCMDUSER) -or [string]::IsNullOrWhiteSpace($env:SQLCMDPASSWORD)) {
        throw 'Set SQLCMDUSER and SQLCMDPASSWORD, or use -UseIntegratedSecurity.'
    }
    $arguments += @('-U', $env:SQLCMDUSER)
}

# SQLCMD performs textual substitution. Encode values to prevent quote injection,
# and keep passwords off command lines and out of output. Hex encoding is not encryption.
function ConvertTo-SqlHex([string] $Value) {
    return '0x' + ([BitConverter]::ToString([Text.Encoding]::Unicode.GetBytes($Value))).Replace('-', '')
}

$variableNames = @('ApiLoginHex', 'ApiUserHex', 'ApiPasswordHex')
$previousValues = @{}
foreach ($name in $variableNames) {
    $previousValues[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

try {
    if (-not $SkipSecurity) {
        if ([string]::IsNullOrWhiteSpace($env:MAINTENANCEBOARD_API_PASSWORD)) {
            throw 'Set MAINTENANCEBOARD_API_PASSWORD before applying the security script.'
        }
        if ($env:MAINTENANCEBOARD_API_PASSWORD.Length -lt 12 -or $env:MAINTENANCEBOARD_API_PASSWORD.Length -gt 128) {
            throw 'MAINTENANCEBOARD_API_PASSWORD must contain between 12 and 128 characters.'
        }
        $login = if ($env:MAINTENANCEBOARD_API_LOGIN) { $env:MAINTENANCEBOARD_API_LOGIN } else { 'MaintenanceBoardApi' }
        $user = if ($env:MAINTENANCEBOARD_API_USER) { $env:MAINTENANCEBOARD_API_USER } else { 'MaintenanceBoardApi' }
        if ($login.Length -gt 128 -or $user.Length -gt 128) {
            throw 'API principal names must not exceed 128 characters.'
        }
        $env:ApiLoginHex = ConvertTo-SqlHex $login
        $env:ApiUserHex = ConvertTo-SqlHex $user
        $env:ApiPasswordHex = ConvertTo-SqlHex $env:MAINTENANCEBOARD_API_PASSWORD
    }

    $scripts = @('001_schema.sql', '002_seed_status_transitions.sql', '003_ticket_procedures.sql')
    if (-not $SkipSecurity) { $scripts += '004_api_security.sql' }

    foreach ($script in $scripts) {
        Write-Host "Applying $script"
        & $sqlcmd.Source @arguments -i (Join-Path $PSScriptRoot $script)
        if ($LASTEXITCODE -ne 0) { throw "Database script failed: $script" }
    }
    Write-Host 'MaintenanceBoard database scripts completed successfully.'
} finally {
    foreach ($name in $variableNames) {
        [Environment]::SetEnvironmentVariable($name, $previousValues[$name], 'Process')
    }
}
