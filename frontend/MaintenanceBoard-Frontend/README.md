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
the API base URL. The owner's committed configuration currently targets the deployed Azure API.
For a local backend, set it to http://localhost:5213/api. This public setting must never contain secrets.

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

The signed-in desktop board calls the real API and displays the three supported status groups.
The login screen is preserved as customized by the project owner.

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
The owner authorized the exported design PDFs as the implementation reference while the MCP quota is unavailable.
The desktop shell follows the exported board layout, adapted to three backend groups and the required application title.

## Desktop board

- A component-scoped Signals store loads the board, cancels superseded requests, and supports retry.
- Summary cards count the complete server snapshot. Column counts reflect local filters.
- Search matches title or ticket ID; status and assignee filters operate on the loaded board.
- Assignee options come from board assignments, so Requesters do not call the restricted users endpoint.
- Real assignee initials, additional assignee counts, and browser-local creation dates are displayed.
- Loading, empty, no-match, and Spanish error states are included.
- New-ticket creation opens a desktop side panel; the entire ticket card opens its details and activity history.
- Date-range filtering and rejected tickets are available in the separate ticket list; mobile layouts are described below.
- Demo ticket titles and descriptions remain exactly as returned by the API, including English seed content.

## Milestone 1 validation

- Production build: passed.
- Basic unit/HTTP checks: 6 passed across 2 test files.
- The sandbox blocked Angular path resolution; both checks passed when rerun with the required filesystem access.
- No API listener was detected on port 5213 during validation. A live login remains to be checked once the API and SQL Server are running.
- No backend or database files were changed by this milestone.

## Milestone 2 validation

- Production build passed within the configured bundle and component-style budgets.
- Basic checks: 9 tests across 3 files, including board loading, authenticated GET, local filtering, empty results, errors, and retry.
- Browser smoke check passed against the running API and SQL Server: Requester login, three real board groups, UPS search, and clearing filters.
- CORS was supplied only through the API process environment for http://localhost:4200; no backend configuration files were edited.
- Desktop output was visually reviewed against the owner-approved exported board reference.
- The API seed reported 0 new users and 0 new tickets. No ticket mutations were performed.
- The owner's login HTML and all pre-existing backend/database changes were preserved and excluded from this milestone commit.

## Ticket creation and detail

The create panel posts only `title` and `description` to `POST /api/tickets`.
Required text is trimmed and limited to 200 and 4000 characters respectively, matching the API request body.
Reporter identity comes from the authenticated header. The API creates the initial Pending status.
Optional assignment in the reference is intentionally deferred to the separate assignments endpoint in milestone 4.
Input remains available after errors; duplicate submissions and closing during an active create request are prevented.
After HTTP 201, the returned ticket ID opens its detail and the board is refreshed with local filters cleared.

Each ticket card has a full-card native button accessible with Tab, Enter, and Space. The detail panel fetches `GET /api/tickets/{id}`
and displays reporter, status, description, cause, all assignees, justifications, and history.
History is shown newest first without changing the API response. Event and status labels are in Spanish.
History remains read-only. The detail now exposes the assignment, transition, and comment actions described below.
Detail reads are canceled on selection change or component destruction; failed reads can be retried.

The reusable side panel uses native `dialog.showModal()` for focus isolation and Escape behavior.
It closes on the close button or Escape and restores native focus behavior. Creation blocks both while submitting.
The create and detail layouts follow the exported desktop PDFs approved by the owner, with mobile adaptations described below.

## Milestone 3 validation

- Production build passed within the configured budgets.
- All 12 basic tests passed across 4 test files.
- Added checks for required fields, exact create payload, duplicate-submit prevention, create errors,
  detail retry, cause, terminal state, and newest-first history.
- JSDOM does not implement native dialog methods; panel tests stub those methods only.
- Permission to start the local API for the browser smoke check was declined.
  No live create request or browser review of this milestone's panels was performed.
- The owner's login HTML, ticket-card CSS, and pre-existing backend/database changes were preserved
  and excluded from the milestone commit.

## Ticket actions

The owner confirmed that the first operator is selected manually, rather than automatically at creation.
The first assignment accepts an optional justification. Each additional assignment requires its own
non-whitespace justification (maximum 2000 characters). The compact `+ Agregar operador` button
registers one additional operator at a time through `POST /api/tickets/{id}/assignments`.
Already assigned users are excluded; the UI enforces the API limit of three distinct assignees.
No removal or replacement action is offered. Assignment is available only on Pending/InProgress tickets
to Operators and Supervisors. Eligible users are loaded lazily through the restricted users endpoint.

Starting attention requires an assignee. Closing requires a cause; rejection requires a reason.
Each transition sends the displayed `expectedStatus` and the allowed target.
Cause, rejection reason, and comments have a 4000-character limit.
Terminal tickets do not expose assignment or status actions.
Requesters may comment only on tickets they reported; Operators and Supervisors may comment on any ticket.
Comments remain available on terminal tickets when the user has permission.

Successful mutations reload detail/history and refresh the board. Pending mutations block duplicate
submissions and panel dismissal. Failed form values are retained. HTTP 409 blocks further mutations
until the user selects `Actualizar ticket`; the interface explains that refreshing discards the pending form.
Native centered dialogs reuse the side-panel focus behavior with unique accessible labels for nested dialogs.
The API remains responsible for authorization, active-user checks, workflow validation, and atomicity.
No actor IDs are sent in mutation bodies.

## Milestone 4 validation

- Production build passed within the configured budgets.
- All 18 basic checks passed across 5 test files.
- Added HTTP/UI checks for first assignment, required per-operator justification, duplicate exclusion,
  cause/rejection validation, expectedStatus, conflict refresh, requester ownership, and terminal/assignment limits.
- Tests use Angular's HTTP testing backend; no live ticket data was mutated.
- Native dialogs still require a browser smoke check. The previously declined API startup was not retried.
- The owner's login, card styles, backend, and database work remain untouched and outside this milestone commit.

## Ticket list and notifications

The sidebar links to the board and the authenticated `/tickets` list.
The list calls `GET /api/tickets` and includes all statuses, including Rejected.
State and date filters are explicitly applied to the server query; title/ID search filters the returned snapshot.
Retry and refresh use the last applied query, not unsaved field edits.
Superseded requests are canceled. Loading, error, empty, and no-match feedback are in Spanish.
Ticket details and creation reuse the existing panels; successful changes reload the current list query.

Date inputs represent inclusive local calendar days. The frontend converts the start to local midnight in UTC
and the end to the next local midnight in UTC, matching the API's exclusive `to` boundary.
The conversion advances the calendar date rather than adding 24 hours, accounting for local DST changes.
Reversed ranges and impossible calendar days are rejected before sending a request.
Optional `status`, `from`, and `to` parameters are omitted when unset.

Toasts provide mutation success and failure feedback in Spanish.
The shared outlet follows the topmost native dialog so feedback stays in its active focus layer.
Success messages expire after seven seconds; errors remain until dismissed, with at most three messages at once.
Inline validation/error details remain available in the forms.
The list uses the desktop shell's existing visual palette and spacing; a separate list frame was not supplied.

## Milestone 5 validation

- Production build passed within the configured budgets.
- All 23 basic checks passed across 7 test files.
- New checks cover rejected-ticket rendering, authenticated list queries, exact filter parameters,
  inclusive calendar dates, month-end/leap-day boundaries, invalid ranges, retry, and notification lifecycle.
- These are HTTP/UI unit checks. Live API and browser verification remain pending; no backend process was started.
- The owner's login HTML, card CSS, backend, and database changes were preserved and excluded from this commit.

## Responsive layouts

The desktop sidebar is retained on large screens and becomes visible horizontal navigation below 1024px.
Below 768px, the board uses state buttons and a single status column with stacked cards.
Filters wrap, controls keep usable touch targets, and detail content becomes a single column.
Dialogs use the available viewport width; action confirmations keep safe screen margins.
The ticket list scrolls horizontally inside its labeled, keyboard-focusable table region without widening the page.
A skip link provides direct keyboard access to the main content.

On mobile, New ticket navigates to the authenticated `/tickets/new` page rather than opening a drawer.
The page reuses the existing form, validation, API client, and feedback.
Its normal document flow supports mobile keyboards and scrolling.
Navigation within the app is blocked while a creation request is pending.
After creation, the app returns to the originating board/list and opens the returned ticket ID.
Only the known board/list return destinations are accepted; detail-link IDs must be positive safe integers.
Desktop creation remains a drawer.

## Milestone 6 validation and remaining review

- Production build passed within the configured budgets.
- All 25 basic checks passed across 8 test files.
- Added checks for mobile status controls, dedicated creation navigation, standalone rendering,
  pending-request navigation protection, and safe return routing.
- HTTP tests explicitly override API_BASE_URL, so the owner's deployed URL does not affect test requests.
- Browser inspection at 390px confirmed the preserved login layout, but the deployed API returned a
  server error during login. Authenticated screen inspection could not proceed.
- A temporary local-data visual preview was proposed but its execution was not approved; no preview files were created.
- Final visual checks of board, list, detail, and nested dialogs at mobile/tablet/desktop widths remain pending.
- No live tickets were created or changed, no local backend was started, and the deployed configuration
  and the owner's login/card styles remain unchanged.

Implementation milestones are committed. The remaining release check is a browser smoke review against
a functioning API, including native dialog focus/Escape behavior and authenticated layouts at 390px, 768px, and desktop widths.
