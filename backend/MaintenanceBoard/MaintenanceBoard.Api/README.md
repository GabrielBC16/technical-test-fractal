# MaintenanceBoard API

Controller-based ASP.NET Core API targeting the SDK in the repository's
`global.json`. Open `../MaintenanceBoard.slnx` in Visual Studio. No Dockerfile is
included; deployment is intended for Azure App Service using a published .NET app.

## Local startup

1. Apply database scripts 001, 002, and 003 using a deployment identity. Reapply
   001 and 003 if using the earlier bootstrap, so the filtered-index session options
   and binary Unicode whitespace guard are current. Script 004 remains deferred
   for the current technical-test setup.
2. Set process environment variables before starting the API or Visual Studio.
   The root `.env.example` is a reference; ASP.NET Core does not load `.env` files.
3. Start the API from the repository root:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__MaintenanceBoard = '<SQL_SERVER_CONNECTION_STRING>'
$env:Cors__AllowedOrigins__0 = '<ANGULAR_ORIGIN>'
dotnet run --project backend/MaintenanceBoard/MaintenanceBoard.Api --launch-profile http
```

The existing HTTP launch profile listens on port 5213. Swagger UI is available
at `/swagger` and the specification at `/swagger/v1/swagger.json` in Development
only. Use **Authorize** to enter the active user ID returned by login. API examples
are provided in `MaintenanceBoard.Api.http`; replace its placeholders first.

Microsoft.OpenApi is explicitly pinned to 2.12.2 instead of SwaggerGen's older
transitive version, addressing the
[circular-reference parsing advisory](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc).

## Environment configuration

Use a single Boolean in appsettings or set the environment variable `DevelopmentSeed`:

```json
"DevelopmentSeed": true
```

- `true`: create the three users below and four sample tickets using those users.
- `false`: do not create users, tickets, assignments, comments, or history.
- Repeated starts preserve existing IDs, passwords, ticket states, and sample tickets.
- Seeding is allowed only in Development and uses the main API database connection.

| User name | Name | Role | Demo password |
| --- | --- | --- | --- |
| `gbarrantes` | Gabriel Barrantes | Requester | `gbarrantes` |
| `fcabello` | Fernando Cabello | Operator | `fcabello` |
| `mhuarcaya` | Mario Huarcaya | Supervisor | `mhuarcaya` |

Gabriel reports all four sample tickets. Fernando attends them; Mario is also
assigned to the Pending sample with a justification. The samples cover Pending,
InProgress, Closed, and Rejected, including causes, rejection reasons, comments,
and history. Passwords are stored as BCrypt hashes. There are no configured user
lists, separate user initializer, optional seed connection, or sample-ticket flag.

| Variable | Purpose |
| --- | --- |
| `ConnectionStrings__MaintenanceBoard` | Required application database connection. |
| `Cors__AllowedOrigins__0` | Allowed Angular origin; add numbered entries for more origins. |
| `ASPNETCORE_ENVIRONMENT` | Development locally; Production in Azure. |
| `ASPNETCORE_URLS` | Optional listening URLs outside launch profiles. |
| `DevelopmentSeed` | The only seed switch; true or false. Defaults to false. |

Restart the API after changing the seed switch. Keep it false in Azure Production.
Without configured CORS origins, browser cross-origin calls are denied. Preflight
requests are anonymous; normal ticket/user requests still require authentication.
CORS headers are retained on business errors and Location is exposed.

## Authentication limitation

**Simulated authentication; replace with JWT/OIDC in production. Only the
authentication handler needs to change.**

`POST /api/auth/login` verifies the BCrypt password hash and returns `userId`,
`userName`, `names`, `lastNames`, and `role`. It does not issue a token. Subsequent
calls send a single positive integer in `X-User-Id`. The handler reloads that user
from SQL, requires an active account, and creates NameIdentifier and Role claims.
Missing, invalid, unknown, or inactive users receive 401; disallowed roles receive 403. The header is an impersonation mechanism for the exercise, not a secure token.

Actor IDs always come from the authenticated principal. Request bodies containing
`reportedByUserId`, `actorUserId`, `assignedByUserId`, or other unmapped fields are
rejected with 400. Only the assignment target `assignedUserId` belongs in its body.

## Endpoints

| Method | Route                           | Authorization / result                                                                       |
| ------ | ------------------------------- | -------------------------------------------------------------------------------------------- |
| POST   | `/api/auth/login`               | Anonymous; returns the verified identity.                                                    |
| POST   | `/api/tickets`                  | Any active role; creates Pending and its initial history event; returns 201 and Location.    |
| GET    | `/api/tickets/board`            | Any active role; Pending, InProgress, and Closed groups.                                     |
| GET    | `/api/tickets`                  | Any active role; optional `status`, `from`, and `to` query parameters. May include Rejected. |
| GET    | `/api/tickets/{id}`             | Any active role; ticket, assignees, cause, and complete chronological history.               |
| POST   | `/api/tickets/{id}/transitions` | Operator/Supervisor; expectedStatus, newStatus, optional comment/cause; returns 204.         |
| POST   | `/api/tickets/{id}/assignments` | Operator/Supervisor; assignedUserId and optional justification; returns 204.                 |
| POST   | `/api/tickets/{id}/comments`    | Requester on own tickets; Operator/Supervisor on any ticket; returns 204.                    |
| GET    | `/api/users`                    | Operator/Supervisor; active eligible assignees only.                                         |

Statuses, roles, and event types are serialized as English strings. Dates are UTC.
The list filter accepts ISO 8601 offsets, converts them to UTC, includes `from`,
and excludes `to`. The detail shape is `{ ticket, cause, history }`; the board shape
is `{ pending, inProgress, closed }`. Refresh these reads after a successful write.

Errors use RFC 7807 ProblemDetails with `type`, `title`, `status`, `detail`,
`instance`, `code`, and `traceId`. Validation failures also include `errors`:

- 400: model binding, malformed JSON, unknown body fields, invalid input/filter.
- 401 / 403: authentication / authorization.
- 404: ticket not found.
- 409: invalid transition, concurrency, assignment limit/duplicate, immutable history.
- 422: missing closure cause, rejection reason, additional-assignee justification,
  invalid assignee, or a business-level empty comment.
- 500: unexpected operational failures only; logged server-side and sanitized.

Basic required-field and length checks run during model validation and return 400.
Conditional workflow guards remain in Application and SQL and return their business
status, such as 422 for a missing closure cause. Controllers contain no workflow rules.

## Azure App Service preparation

Publish a framework-dependent app without a platform-specific launcher:

```powershell
dotnet publish backend/MaintenanceBoard/MaintenanceBoard.Api --configuration Release --output .artifacts/publish/api -p:UseAppHost=false
```

Deploy that output to an App Service with a compatible .NET 10 runtime. Configure
the variables above in App Service environment settings, set Production, keep
development seeding disabled, and configure the actual Angular origin. Do not
publish development credentials. Swagger is intentionally unavailable in Production.

Enable **HTTPS Only** in App Service. The hosting platform terminates TLS; the API
does not redirect the internal HTTP hop. Created-resource Locations are relative,
so they remain valid behind the platform proxy. Azure resource provisioning,
network/database access configuration, and deployment are outside this milestone.
