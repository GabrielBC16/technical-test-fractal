# Infrastructure milestone

This project implements Application ports with Dapper, Microsoft.Data.SqlClient,
and BCrypt.Net-Next. Its only project reference is Application; Domain is available
through that reference. No EF Core, Identity, MediatR, or CQRS framework is used.

## Persistence

- `SqlConnectionFactory` receives its connection string from the composition root.
  It requires an explicit application database and never embeds connection settings.
- `SqlExecutor` owns and disposes each pooled connection and translates SQL business
  errors 50001–50014 into the corresponding Application exceptions. Unexpected SQL
  and connection errors remain operational failures for the API to log and sanitize.
- `SqlTicketRepository` performs every ticket write through the four ticket SPs.
  Actor IDs are supplied by Application, not parsed from request bodies here.
- Reads use constant SQL and Dapper parameters. Board reads exclude Rejected in SQL;
  general listings allow it. Date filters use datetime2 parameters with inclusive
  lower and exclusive upper UTC boundaries. Lists fetch assignments in one extra
  result set instead of issuing one query per ticket.
- Detail and list batches use short serializable read transactions to keep their
  result sets coherent. UTC metadata is restored when mapping SQL datetime2 values.
  History is ordered by CreatedAtUtc and HistoryId and retains inactive users' names.
- `SqlUserRepository` separates credential reads from public user data and returns
  only active Operators/Supervisors for assignee selection.

Register these adapters using `AddInfrastructure(connectionString)` in the API
composition root. Application services, TicketTransitionPolicy, authentication,
and ICurrentUser remain registrations for the API milestone.

## Password hashes

`BCryptPasswordHasher` generates random salts at cost 11 by default. Passwords are
not trimmed. Standard BCrypt accepts at most 72 UTF-8 bytes; longer inputs are
rejected instead of silently truncated. Invalid or unsupported hashes fail
verification without becoming an HTTP 500. The cost-4 constructor setting is used
only by automated tests; the API default remains 11.

The explicit Pkcs 10.0.13 reference overrides SqlClient's vulnerable transitive
version. See the Microsoft advisories for
[CVE-2026-84008](https://github.com/advisories/GHSA-846f-f764-76mx) and
[CVE-2026-70162](https://github.com/advisories/GHSA-2fmv-rv2c-59x7).

## Development data

`DevelopmentSeeder.SeedAsync(environmentName, options)` refuses to run outside
Development. The API milestone will wire its opt-in environment configuration;
no automatic startup seeding has been enabled in this milestone.

Supply `DevelopmentSeedOptions` with usernames, names, roles, and passwords loaded
from configuration. No application credentials are hardcoded. Include at least one
Requester and one Operator/Supervisor to generate sample tickets. Seeding:

1. Acquires a transaction-owned application lock to serialize simultaneous starts.
2. Inserts missing users with BCrypt hashes using the deployment/seed identity.
3. Preserves existing users and passwords, rejecting role mismatches or inactive users.
4. Optionally creates four `[Demo]` tickets through SPs, covering all four states,
   assignments, a multi-assignee justification, causes, reasons, and comments.
5. Commits users and sample tickets together, or rolls back the entire seed.

Matching existing demo titles for the same reporter are left unchanged. Seeding
does not reset an existing ticket's state. The seed identity requires INSERT on
access.AppUser; it is separate from a restricted runtime identity when script 004
is enabled. For the current technical-test setup, script 004 may remain deferred.

## Validation

Run unit tests using the existing Visual Studio solution or the CLI:

```powershell
dotnet test backend/MaintenanceBoard/MaintenanceBoard.Tests/MaintenanceBoard.Tests.csproj --configuration Release
```

Real SQL tests are opt-in. Load `MAINTENANCEBOARD_TEST_CONNECTION_STRING` from your
local environment and run the same command. The selected database name must start
with `MaintenanceBoard_Validation`. Tests intentionally refuse application databases,
insert their own isolated fixtures, and leave them available for inspection. The
connection uses a deployment identity because validation creates a temporary
history-failure trigger to prove rollback. Never use a production database.

Without the test environment variable, SQL integration cases are reported as skipped.
No SQL server, login, password, or connection string is stored in the test source.
