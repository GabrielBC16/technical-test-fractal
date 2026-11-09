[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $Server,
    [Parameter(Mandatory = $true)] [string] $Database
)

$ErrorActionPreference = 'Stop'
if (-not $Database.StartsWith('MaintenanceBoard_Validation', [StringComparison]::Ordinal)) {
    throw 'Use an isolated database named MaintenanceBoard_Validation followed by a suffix.'
}
$sqlcmdPath = (Get-Command sqlcmd -ErrorAction Stop).Source
$runId = [Guid]::NewGuid().ToString('N')

# Only the internally generated hexadecimal run ID is interpolated into fixture SQL.
$fixture = @"
SET NOCOUNT ON;
IF DB_NAME() NOT LIKE N'MaintenanceBoard[_]Validation%'
    THROW 51000, N'An isolated validation database is required.', 1;
DECLARE @Users TABLE (UserId int, Names nvarchar(100));
INSERT INTO access.AppUser (UserName, PasswordHash, Names, LastNames, Role, IsActive)
OUTPUT inserted.UserId, inserted.Names INTO @Users
VALUES
    (N'race-a-$runId', N'ValidationOnly_NotAnAuthenticationHash', N'A', N'Validation', N'Operator', 1),
    (N'race-b-$runId', N'ValidationOnly_NotAnAuthenticationHash', N'B', N'Validation', N'Operator', 1),
    (N'race-c-$runId', N'ValidationOnly_NotAnAuthenticationHash', N'C', N'Validation', N'Operator', 1),
    (N'race-d-$runId', N'ValidationOnly_NotAnAuthenticationHash', N'D', N'Validation', N'Supervisor', 1);
DECLARE @A int=(SELECT UserId FROM @Users WHERE Names=N'A');
DECLARE @B int=(SELECT UserId FROM @Users WHERE Names=N'B');
DECLARE @C int=(SELECT UserId FROM @Users WHERE Names=N'C');
DECLARE @D int=(SELECT UserId FROM @Users WHERE Names=N'D');
DECLARE @Created TABLE (TicketId int);
INSERT INTO @Created EXEC tickets.usp_CreateTicket N'Concurrent assignment validation', N'Validation-only race fixture.', @A;
DECLARE @TicketId int=(SELECT TicketId FROM @Created);
EXEC tickets.usp_AssignTicket @TicketId, @A, @A;
EXEC tickets.usp_AssignTicket @TicketId, @B, @A, N'Second specialist required.';
SELECT CONCAT(@TicketId, N'|', @A, N'|', @C, N'|', @D);
"@

$fixtureResult = & $sqlcmdPath -S $Server -d $Database -E -b -r 1 -h -1 -W -Q $fixture 2>&1
if ($LASTEXITCODE -ne 0) { throw "Fixture setup failed: $fixtureResult" }
$values = (($fixtureResult | ForEach-Object { "$_" }) -match '^\d+\|\d+\|\d+\|\d+$')
if (@($values).Count -ne 1) { throw 'Could not read the validation fixture IDs.' }
$ids = ([string] $values).Split('|') | ForEach-Object { [int] $_ }
$ticketId, $actorId, $candidateOne, $candidateTwo = $ids

$worker = {
    param($SqlcmdPath, $ServerName, $DatabaseName, [int] $TicketId, [int] $ActorId, [int] $CandidateId)
    # Keep the successful transaction open briefly so the other session contends
    # for the same parent lock before it can count existing assignments.
    $command = "WAITFOR DELAY '00:00:01'; BEGIN TRANSACTION; EXEC tickets.usp_AssignTicket $TicketId, $CandidateId, $ActorId, N'Concurrent additional assignment'; WAITFOR DELAY '00:00:03'; COMMIT TRANSACTION;"
    $output = & $SqlcmdPath -S $ServerName -d $DatabaseName -E -b -r 1 -Q $command 2>&1
    [pscustomobject] @{ ExitCode = $LASTEXITCODE; Output = ($output | Out-String) }
}

$jobs = @()
try {
    $jobs += Start-Job -ScriptBlock $worker -ArgumentList $sqlcmdPath, $Server, $Database, $ticketId, $actorId, $candidateOne
    $jobs += Start-Job -ScriptBlock $worker -ArgumentList $sqlcmdPath, $Server, $Database, $ticketId, $actorId, $candidateTwo
    $null = $jobs | Wait-Job -Timeout 45
    if (@($jobs | Where-Object State -ne 'Completed').Count -ne 0) {
        throw 'Concurrent assignment workers did not complete within 45 seconds.'
    }
    $results = @($jobs | Receive-Job)
    if (@($results | Where-Object ExitCode -eq 0).Count -ne 1 -or
        @($results | Where-Object { $_.ExitCode -ne 0 -and $_.Output -match '50006' }).Count -ne 1) {
        throw "Expected one successful assignment and one SQL error 50006. Results: $($results | ConvertTo-Json -Compress)"
    }

    $query = "SELECT CONCAT((SELECT COUNT(*) FROM tickets.TicketAssignment WHERE TicketId=$ticketId), N'|', (SELECT COUNT(*) FROM tickets.TicketHistory WHERE TicketId=$ticketId AND EventType=N'Assigned'));"
    $counts = & $sqlcmdPath -S $Server -d $Database -E -b -h -1 -W -Q $query 2>&1
    if ($LASTEXITCODE -ne 0 -or -not (($counts | ForEach-Object { "$_".Trim() }) -contains '3|3')) {
        throw 'Concurrent writes must leave exactly three assignments and three assignment history events.'
    }
    Write-Host 'PASS: concurrent assignment requests cannot exceed three assignees.'
} finally {
    $jobs | Stop-Job -ErrorAction SilentlyContinue
    $jobs | Remove-Job -Force -ErrorAction SilentlyContinue
}
