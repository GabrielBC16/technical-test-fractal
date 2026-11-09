# MaintenanceBoard

MaintenanceBoard is a web application for reporting and tracking maintenance incidents, developed for the Fractal technical challenge. Requesters report problems; operators and supervisors assign responsibility, investigate incidents, and close or reject tickets with a traceable activity history.

The interface is in Spanish and displays **Tablero de tickets de mantenimiento**. Source code, API contracts, documentation, and technical identifiers are in English. Seeded ticket content is in Spanish.

## Features

- Responsive Kanban board with Pending, InProgress, and Closed columns.
- Ticket creation in a desktop side panel and a dedicated mobile page.
- Ticket details with assigned users, root cause, comments, and chronological history.
- Assignment to up to three operators or supervisors, with justification for additional assignees.
- Ticket list with status/date filters, including rejected tickets, and local title/ID search.
- Role-aware actions, form validation, loading/empty/error states, and toast feedback.
- Transactional persistence and concurrency checks for ticket operations.

## Architecture

The system consists of an Angular client and a controller-based ASP.NET Core API backed by SQL Server. The backend is a **modular monolith** with `Tickets` and `Access` modules organized as folders within its layers.

| Part | Technology and responsibility |
| --- | --- |
| Frontend | Angular 21, TypeScript, Tailwind CSS 4, standalone components, Signals, reactive forms, and HttpClient/RxJS. |
| API | .NET 10, thin controllers, header authentication, authorization, Swagger in Development, and global ProblemDetails error handling. |
| Application | Use cases, repository interfaces, ownership/assignment validation, and application exceptions. |
| Domain | Entities, enums, and the pure `TicketTransitionPolicy`; no external project dependencies. |
| Infrastructure | Dapper, Microsoft.Data.SqlClient, stored procedure calls, BCrypt password hashing, and development seeding. |
| Database | SQL Server with `access` and `tickets` schemas, named constraints, indexes, and transactional stored procedures. |

Project dependencies are `Api -> Application + Infrastructure`, `Infrastructure -> Application`, and `Application -> Domain`. Repository interfaces live in Application and are implemented in Infrastructure. No EF Core, ASP.NET Core Identity, MediatR, or CQRS framework is used.

This structure keeps workflow rules independently testable while using explicit SQL for persistence and concurrency. Angular Signals manage client state, and reusable components provide a consistent responsive interface.

```text
backend/
  MaintenanceBoard/
    MaintenanceBoard.slnx
    MaintenanceBoard.Domain/
    MaintenanceBoard.Application/
    MaintenanceBoard.Infrastructure/
    MaintenanceBoard.Api/
    MaintenanceBoard.Tests/
database/
  001_schema.sql
  002_seed_status_transitions.sql
  003_ticket_procedures.sql
frontend/
  MaintenanceBoard-Frontend/
prompts/
  PROMPTS.md
```

### Data model and persistence

- `access.AppUser` stores identities, roles, active status, and BCrypt password hashes.
- `tickets.Ticket` stores the reporter, title, description, status, cause, and timestamps.
- `tickets.TicketAssignment` associates tickets with assigned users and records who assigned them and why.
- `tickets.TicketHistory` records creation, transitions, assignments, and comments; it is append-only.
- `tickets.TicketStatusTransition` contains the four permitted transitions.

Each ticket has many history events and up to three assignments. All timestamps are UTC. Ticket writes go through stored procedures using explicit transactions, `XACT_ABORT`, and rollback on failure. Creating a ticket also creates its initial history event; each successful transition updates the ticket and inserts exactly one history event atomically. A failed history insertion rolls back the operation.

Application services validate first. Stored procedures recheck the rules under database locks as the final guard. Status transitions compare the actual state with `expectedStatus` under `UPDLOCK, HOLDLOCK`; stale requests receive a conflict rather than overwriting a newer state.

## Business rules

### Ticket lifecycle

| Current status | Allowed next status | Requirement |
| --- | --- | --- |
| `Pending` | `InProgress` | At least one assigned user. |
| `Pending` | `Rejected` | A nonempty rejection comment. |
| `InProgress` | `Closed` | A nonempty `Cause` describing the root cause. |
| `InProgress` | `Rejected` | A nonempty rejection comment. |
| `Closed` / `Rejected` | None | Terminal statuses. |

Tickets start in `Pending`. Unknown states, skipped steps, backward transitions, and transitions to the same state are rejected. Rejected tickets are excluded from the main board and remain accessible through the ticket list and details.

### Roles and assignments

| Action | Requester | Operator / Supervisor |
| --- | --- | --- |
| Create tickets | Yes | Yes |
| View board, list, and details | Yes | Yes |
| Add comments | Only on tickets they reported | On any ticket |
| Assign users and change status | No | Yes, on any ticket |
| List eligible assignees | No | Yes |

- A reporter is not automatically an assignee.
- Assignments are allowed only in `Pending` or `InProgress`.
- Assignees must be active Operators or Supervisors; self-assignment is allowed.
- A ticket has at most three distinct assignees. Duplicate assignments are rejected.
- The first assignment requires no justification; the second and third each require a nonempty justification.
- Comments add history without changing status and are permitted on terminal tickets subject to the same role/ownership rules.
- Actor and reporter IDs come from the authenticated principal, never from request bodies. `assignedUserId` identifies the assignment target.

### Authentication and error handling

Login verifies a BCrypt password hash and returns the user's identity and role. The frontend stores that identity in tab-scoped `sessionStorage`, never the password, and sends `X-User-Id` on subsequent API requests. The API reloads the active user and applies role and ownership checks.

**Simulated authentication; replace with JWT/OIDC in production. Only the authentication handler needs to change.** The user ID header is an impersonation mechanism for this exercise, not a secure authentication token.

Errors use RFC 7807 ProblemDetails: `400` for invalid requests, `401` for missing/invalid authentication, `403` for denied actions, `404` for missing tickets, `409` for workflow/concurrency conflicts, and `422` for conditional business validation. Unexpected operational errors are logged and returned with sanitized details.

## Run the complete application locally

The following steps start both the API and frontend against a local database. Commands use **Windows PowerShell** and start from the repository root unless stated otherwise.

### 1. Install prerequisites and clone

- Git.
- The **.NET SDK specified in [global.json](global.json)**: `10.0.403`, with the configured patch roll-forward policy. Installing only the runtime is insufficient.
- A running SQL Server Database Engine and SQL Server Management Studio (SSMS). SSMS alone does not install the database engine.
- Node.js 24 and npm; the frontend declares npm `11.12.1`.
- Optionally, Visual Studio with ASP.NET/web development tools and support for the pinned SDK and `.slnx` solution format.

```powershell
git clone https://github.com/GabrielBC16/technical-test-fractal.git
cd technical-test-fractal
dotnet --version
node --version
npm --version
```

### 2. Create the database and apply the SQL scripts

Connect to your SQL Server instance in SSMS using a development account with permission to create the database and its objects.

1. Open [001_schema.sql](database/001_schema.sql). For a fresh installation, select and execute its `CREATE DATABASE MaintenanceBoard;` statement **by itself** first.
2. Select and execute the rest of that file, starting at `USE MaintenanceBoard;`, to create the schemas, tables, constraints, indexes, and history protection trigger.
3. With `MaintenanceBoard` selected as the database, execute [002_seed_status_transitions.sql](database/002_seed_status_transitions.sql).
4. In the same database, execute [003_ticket_procedures.sql](database/003_ticket_procedures.sql).

If the database already exists, skip the `CREATE DATABASE` statement. The schema script preserves existing tables; it is a bootstrap script, not a general migration tool. Scripts 002 and 003 do not create application users or sample tickets; API startup performs development seeding in the next step.

### 3. Configure and start the API

In PowerShell at the repository root:

```powershell
# Use '.' for the default local instance or '.\SQLEXPRESS' for SQL Express.
$sqlServer = '.'
$env:ConnectionStrings__MaintenanceBoard = "Server=$sqlServer;Database=MaintenanceBoard;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:5213'
$env:DevelopmentSeed = 'true'

dotnet restore backend/MaintenanceBoard/MaintenanceBoard.slnx
dotnet run --project backend/MaintenanceBoard/MaintenanceBoard.Api --no-launch-profile
```

This example uses Windows authentication; the Windows account running the API must have access to the database and permission to insert development users and execute ticket procedures. For SQL authentication, replace the connection string through the environment with credentials for your own server. Keep credentials out of source control. `TrustServerCertificate=True` is for a local development certificate.

**Keep `--no-launch-profile`: the committed launch profiles currently set `ASPNETCORE_ENVIRONMENT` to `Production`.** Bypassing them allows the environment variables above to enable development seeding and Swagger.

Open [Swagger](http://localhost:5213/swagger). Keep the API terminal running. Environment variables apply to that terminal and its child processes; a `.env` file is not loaded automatically.

For Visual Studio, open `backend/MaintenanceBoard/MaintenanceBoard.slnx`, set `MaintenanceBoard.Api` as the startup project, and configure the selected launch profile with `ASPNETCORE_ENVIRONMENT=Development` and the same local connection/seed settings before running. Keep machine-specific credentials outside tracked files.

### 4. Configure and start Angular

In a **second terminal** at the repository root:

```powershell
cd frontend/MaintenanceBoard-Frontend
npm ci
```

The committed frontend configuration points to the Azure API. To run the complete stack locally, change `src/environments/environment.ts` to:

```typescript
export const environment = {
  apiBaseUrl: 'http://localhost:5213/api',
};
```

Keep the `/api` suffix. Then run:

```powershell
npm start
```

Open [http://localhost:4200](http://localhost:4200). If PowerShell blocks `npm.ps1`, use `npm.cmd ci` and `npm.cmd start` instead.

The current backend CORS policy allows any origin for the technical demonstration; `Cors__AllowedOrigins__0` is not currently enforced. Restrict the policy explicitly before using this application beyond the exercise. Frontend configuration is public and must never contain database credentials.

### 5. Sign in and verify the workflow

With `DevelopmentSeed=true` in Development, startup creates the following accounts and four sample tickets, one per status. Ticket descriptions, causes, justifications, and comments are in Spanish. Existing users and matching sample tickets are preserved on subsequent starts.

| Name | Username | Demo password | Role |
| --- | --- | --- | --- |
| Gabriel Barrantes | `gbarrantes` | `gbarrantes` | Requester |
| Fernando Cabello | `fcabello` | `fcabello` | Operator |
| Pedro Ramirez | `pramirez` | `pramirez` | Operator |
| Juan Carlos Cordova | `jcordova` | `jcordova` | Operator |
| Mario Huarcaya | `mhuarcaya` | `mhuarcaya` | Supervisor |

1. Sign in as Gabriel and create a ticket.
2. Sign out and sign in as Fernando or Mario. Assign an eligible user, then start the ticket.
3. Add a comment and close the ticket with a root cause. Open its details to inspect the history.
4. Use another open ticket to test rejection; confirm it disappears from the board and remains in the filtered list.

For Swagger, call `POST /api/auth/login`, copy the returned `userId`, and enter it using **Authorize**. Do not assume fixed IDs.

Set `DevelopmentSeed=false` to stop seeding on later starts. This does not delete existing data. Seeding outside Development is rejected; leave it disabled in Azure Production.

## Build and tests

From the repository root, run backend tests that do not require a real SQL Server connection:

```powershell
dotnet build backend/MaintenanceBoard/MaintenanceBoard.slnx --configuration Release
dotnet test backend/MaintenanceBoard/MaintenanceBoard.slnx --filter "Category!=Integration"
```

The test suite covers transition policy rules, application services with fake repositories, password handling, SQL mappings, and API behavior. Real SQL integration tests require `MAINTENANCEBOARD_TEST_CONNECTION_STRING` pointing to a separately prepared database whose name starts with `MaintenanceBoard_Validation`; they write test data and must not target the application database. They are skipped when the variable is absent. See the [integration test setup](backend/MaintenanceBoard/MaintenanceBoard.Tests/Integration/SqlIntegrationContext.cs) for the enforced configuration.

For the frontend:

```powershell
cd frontend/MaintenanceBoard-Frontend
npm run build
npm test -- --watch=false
```

Angular tests use an HTTP testing backend. The production build is written under `dist/MaintenanceBoard-Frontend/`. The current Angular configuration has no environment file replacement; check the API URL in `environment.ts` before building for a particular deployment.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| The pinned .NET SDK cannot be found | Install the SDK selected by `global.json` and restart the terminal or Visual Studio. |
| SQL connection fails | Confirm the SQL Server service, instance name, authentication, database name, and account permissions. |
| Connection string format error | Supply a complete SQL connection string, not a placeholder or just a server name. |
| Seed startup fails or Swagger is unavailable | Use `--no-launch-profile` with Development as shown above. Production intentionally disables Swagger and refuses seeding. |
| Login fails on a fresh database | Apply scripts 001–003 and start with `DevelopmentSeed=true`. Existing account passwords are not reset by seeding. |
| Angular still calls Azure | Set `environment.ts` to the local `/api` URL and restart Angular. |
| An action returns 403, 409, or 422 | Check the current role, ticket state, required fields, and assignment rules. Refresh details after a concurrent change. |

## Further documentation

- [Backend guide](backend/README.md): API endpoints, payload conventions, authentication, and error contracts. Use the root startup steps above for the current launch-profile behavior.
- [Frontend guide](frontend/README.md): components, routes, client state, and frontend-specific setup.
- [Infrastructure notes](backend/MaintenanceBoard/MaintenanceBoard.Infrastructure/README.md): persistence adapter and hashing implementation details.
- [Prompt log](prompts/PROMPTS.md): recorded AI assistance prompts.

The application has been deployed to Azure. The configured hosted API URL is recorded in the frontend environment file; the steps above run the complete stack locally. User CRUD, unassignment, and real-time notifications are outside this implementation's scope.
