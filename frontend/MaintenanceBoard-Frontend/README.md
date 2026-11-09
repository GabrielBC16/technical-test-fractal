# MaintenanceBoard frontend

Angular 21 standalone components, signals, reactive forms, and Tailwind CSS 4.
Source code, comments, and documentation are in English; interface text is in Spanish.

## Run locally

Use a Node.js version supported by the installed Angular 21 CLI (Node 22.12+ or 24).
From this directory:

```powershell
npm ci
npm start
```

Open http://localhost:4200. Configure `src/environments/environment.ts` to change
the API base URL (default: http://localhost:5213/api). This public setting must never contain secrets.

Start the existing API from `backend/MaintenanceBoard/MaintenanceBoard.Api`
using the backend README. It requires a configured SQL Server connection and applied database schema.
For local use, set `Cors__AllowedOrigins__0=http://localhost:4200` in the API process.
Development demo users require `DevelopmentSeed=true` in the Development environment.
Do not commit connection strings or passwords.

## Authentication

The login form calls `POST /api/auth/login` with `userName` and `password`.
Only the returned identity is stored in tab-scoped sessionStorage; passwords are never persisted.
If storage is unavailable, the session lasts until page reload.
The interceptor sends the returned integer `userId` as `X-User-Id` only to the configured API,
excluding login. A protected API response with status 401 clears the matching session.
HTTP 403 leaves the session intact. Navigation guards are convenience checks; the API enforces permissions.

Simulated authentication; replace with JWT/OIDC in production. Only the authentication handler needs to change.

The login screen and signed-in landing page are functional foundation screens.
The landing page explicitly indicates that the ticket board belongs to the next milestone.
They are not claimed as a completed Figma implementation.

## Validation

```powershell
npm run build
npm test -- --watch=false
```

Basic tests cover login identity, password preservation without persistence, invalid storage,
interceptor scope, unauthorized responses, and navigation permissions.
HTTP tests use Angular's testing backend and do not prove connectivity to SQL or a running API.

## Milestones

1. Foundation and simulated session: API configuration, login, logout, guards, interceptor, Spanish errors.
2. Desktop shell and live board: match the Figma reference, fetch the three backend groups.
3. Ticket creation and details: desktop drawer, validation, history.
4. Ticket actions: assignments, justification, comments, conditional transitions and concurrency recovery.
5. Ticket list and filters: include rejected tickets, local dates, toasts, loading/empty/error states.
6. Responsive layouts and final review: mobile creation page and accessibility checks.

Each milestone ends with basic checks, a Conventional Commit, and a short handoff.

## Backend alignment

Contracts were checked against the controllers, request bodies, authentication models, and ticket read models.
Roles are `Requester`, `Operator`, and `Supervisor`; JSON enums are strings.
The API rejects unknown request members, so actor identity must never be included in mutation bodies.
The board endpoint returns `pending`, `inProgress`, and `closed` only.
Although the desktop reference includes a rejected column, rejected tickets belong in the filtered list.
Dates use an inclusive lower bound and exclusive upper bound in UTC.
The frontend must display UTC timestamps in local time.

Design reference: https://www.figma.com/design/nbxTP0vNiHRD77P0uWRFg5/Fractal-%7C-Maintenance---Tickets?node-id=23-2294
The reference was visually accessible in the browser; MCP design extraction hit the Starter plan limit.
High-fidelity context and assets still need extraction before implementing the Figma desktop screens.

## Milestone 1 validation

- Production build: passed.
- Basic unit/HTTP checks: 6 passed across 2 test files.
- The sandbox blocked Angular path resolution; both checks passed when rerun with the required filesystem access.
- No API listener was detected on port 5213 during validation. A live login remains to be checked once the API and SQL Server are running.
- No backend or database files were changed by this milestone.
