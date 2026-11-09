# MaintenanceBoard Frontend

MaintenanceBoard is a maintenance ticket application built with Angular 21, TypeScript, and Tailwind CSS 4. The Angular project is in `MaintenanceBoard-Frontend/`. Interface text is in Spanish; source code, comments, and documentation are in English.

## Architecture overview

The application uses standalone components, lazy-loaded routes, Angular Signals for reactive state, reactive forms for input validation, and RxJS for HTTP request handling and cancellation. It consumes the existing ASP.NET Core API of the backend, through Angular HttpClient.

The main areas under `MaintenanceBoard-Frontend/src/app/` are:

| Area                  | Responsibility                                                           |
| --------------------- | ------------------------------------------------------------------------ |
| `core/auth/`          | Simulated session, session/role guards, and the `X-User-Id` interceptor. |
| `core/api/`           | Injectable API base URL and Spanish error messages.                      |
| `core/tickets/`       | API contracts, HTTP client, date boundaries, and presentation helpers.   |
| `core/layout/`        | Mobile breakpoint detection.                                             |
| `core/notifications/` | Shared toast notification state and lifecycle.                           |
| `layout/`             | Desktop sidebar, mobile navigation, account information, and logout.     |
| `features/`           | Login, board, ticket list, creation, details, and ticket actions.        |
| `shared/`             | Reusable side panels/dialogs and toast outlet.                           |

### Main components

- **Login** authenticates against the API and stores only the returned identity in tab-scoped sessionStorage. Passwords are not persisted.
- **DesktopShell** provides the shared navigation and account layout, adapting to smaller screens.
- **Board / BoardStore / TicketCard** load the three board groups: Pending, InProgress, and Closed. The entire ticket card opens its detail. Mobile uses status buttons and stacked cards.
- **TicketList** includes all statuses, including Rejected, with server-side status/date filters and local title/ID search.
- **CreateTicket** provides the validated creation form in a desktop drawer. **CreateTicketPage** reuses it as a separate mobile page.
- **TicketDetail** displays the ticket, cause, assigned operators, and activity history.
- **TicketActions** manages assignments, comments, and allowed status transitions. The first operator is selected manually; each additional operator requires its own justification, with a maximum of three assignees.
- **SidePanel / ToastOutlet** provide native modal behavior and operation feedback, including notifications inside active dialogs.

Protected routes are `/board`, `/tickets`, and `/tickets/new`. `/login` is the public entry point. Client guards control navigation; the backend remains responsible for authorization and business rules.

Simulated authentication; replace with JWT/OIDC in production. Only the authentication handler needs to change.

## Run the frontend locally

### 1. Install prerequisites

Install Node.js and npm. Development and validation used Node.js 24. The project declares npm 11.12.1 in `package.json`.

Check your installation:

```powershell
node --version
npm --version
```

You do not need a globally installed Angular CLI; the npm scripts use the project's local CLI.

### 2. Open the Angular project directory

From the repository root:

```powershell
cd frontend/MaintenanceBoard-Frontend
```

Run all following npm commands from this directory, where `package.json` is located.

### 3. Install dependencies

```powershell
npm ci
```

This installs the versions recorded in `package-lock.json`.

### 4. Select the API

Open `src/environments/environment.ts`. The committed configuration points to the deployed Azure API. Keep that value to use the hosted backend; no local .NET or SQL Server installation is needed for that option.

To use a local backend instead, set:

```typescript
export const environment = {
  apiBaseUrl: "http://localhost:5213/api",
};
```

Keep the `/api` suffix. This is public frontend configuration, so it must not contain connection strings or secrets. Restart the development server after changing the API configuration if the update is not reflected.

For a local API, first follow the setup instructions in [the backend README](../backend/MaintenanceBoard/MaintenanceBoard.Api/README.md), including its SQL Server requirements.

### 5. Start Angular

```powershell
npm start
```

Open [http://localhost:4200](http://localhost:4200). The application redirects to login when there is no session. Keep this terminal running; press Ctrl+C to stop the development server.

If PowerShell blocks `npm.ps1`, use `npm.cmd ci` and `npm.cmd start` without changing your execution policy.

### 6. Sign in

Use an active account available in the selected backend. When its Development seed is enabled, the documented demo accounts are:

| Username     | Password     | Role       |
| ------------ | ------------ | ---------- |
| `gbarrantes` | `gbarrantes` | Requester  |
| `fcabello`   | `fcabello`   | Operator   |
| `pramirez`   | `pramirez`   | Operator   |
| `jcordova`   | `jcordova`   | Operator   |
| `mhuarcaya`  | `mhuarcaya`  | Supervisor |

Demo account availability depends on the backend environment and seed configuration. Angular does not create users. Each authenticated API request sends the actual user ID returned by login; there is no JWT.

## Basic verification

From `frontend/MaintenanceBoard-Frontend/`:

```powershell
npm run build
npm test -- --watch=false
```

The production build is generated under `dist/MaintenanceBoard-Frontend/`. Tests use an HTTP testing backend and an explicitly overridden API URL; they do not create or change real tickets.

If login fails, check that the configured API is available, the credentials are valid, and CORS rules are open for all. A server error from the hosted API requires backend diagnosis. An Angular development server can start successfully while its API is unavailable.
