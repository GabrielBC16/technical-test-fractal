## Initial PROMPT FOR PROGRAMATION ACTIVITES

Act as a Senior Tech Lead and Project Manager expert in .NET (C#), Angular, and SQL Server architectures.

I need you to design a detailed, prioritized day-by-day work plan to complete the "Maintenance Control and Tickets" Technical Challenge from the Fractal platform. The full details can be found in the attached PDF document named DTP-L2_Tickets-Mantenimiento_ES.pdf.

PROJECT CONTEXT & CONSTRAINTS:
Current status: Today is DAY 1 of the project.

Chosen Tech Stack:

Backend: .NET (C#) Web API (.NET 8/9).

Frontend: Angular (recent version, Standalone Components / Signals).

Database: SQL Server (using Stored Procedures for status transitions and history tracking).

Deployment: Azure / AWS / Railway.

Mandatory "English First" Rule: All code (variables, methods, commits), README, comments, and documentation must strictly be written in ENGLISH.

Target Score: Pass rate over 70/100, targeting 100/100 across core requirements plus the 10-point BONUS.

MANDATORY DELIVERABLES ACCORDING TO THE DOCUMENT:
Source code (Public repository with progressive commits in English).

Deployed App (Public, fully functional end-to-end URL).

UI Mockup / Prototype.

Sequence Diagram (User/Operator -> Frontend -> API -> SP/DB).

Data Model / ERD (Ticket 1 - N History).

PROMPTS.md file recording all prompts used.

README.md in English with local execution guide and stack justification.

Oral defense presentation prep (in English) for the committee.

WHAT YOU MUST GENERATE IN YOUR RESPONSE:

1. Detailed Day-by-Day Schedule (Starting TODAY - DAY 1 through DAY 3):
   Include the following activities and any others you deem essential for development:

DAY 1 (Design & Planning - TODAY):

Architecture definition (Clean Architecture / N-Layering in .NET).

SQL Server data structure design (Tickets and History tables with 1-N relational integrity).

ERD and Sequence Diagram creation (PlantUML or Mermaid templates).

Backend business rules definition (State Pattern or Status Validators).

UI Prototype / Mockup design.

DAY 2.1 (Functional Core & Repository):

Repository initialization (GitHub) with initial directory structure.

SQL Server DB implementation + mandatory Stored Procedures (create ticket, change status, log history).

.NET Backend development (Controllers, Services, Global Exception Handling to avoid generic HTTP 500 errors).

Initial Angular Frontend development (Creation form and Kanban board / status list components).

Checkpoint: First push of the functional core to the repository.

DAY 2.2 (Integration, Advanced Rules & Bonus):

Full Frontend <-> Backend integration.

UI validation handling and API error capturing.

BONUS features implementation (+10 pts): Operator assignment to tickets, status/date visual filters, and notification system (toast/simulated).

Data seeding scripts creation for testing.

DAY 3 (Deployment, Documentation & Presentation Prep):

Backend, Frontend, and Database deployment to a public cloud service (functional URL).

Final writing of README.md in English and consolidation of PROMPTS.md.

Defense pitch preparation in English (Architecture justification, stack choices, most difficult technical challenge).

2. Acceptance Criteria Verification Checklist:
   A quick audit checklist to verify before final submission to ensure no rubric requirement (R2.1 to R2.12) is missed.

3. Visual Sequence Map (Mermaid Diagram):
   Additionally, structure all plan tasks into a Visual Sequence Map using Mermaid code (Gantt chart or Flowchart).

Diagram Structure Requirements:

CLEAR SEQUENTIAL ORDER: Strictly dependent tasks must show direct sequence (e.g., Create SQL Server tables -> Create Stored Procedures -> Connect .NET API).

PARALLEL TASKS IDENTIFIED: Clearly identify tasks that can run in parallel or alternate without blocking (e.g., designing Figma/Excalidraw mockups while compiling SP logic in SQL Server; or developing Angular components while setting up .NET DTOs and validators).

DAY 1 TO DAY 3 BREAKDOWN: Visually group tasks by phases/days (Day 1: Analysis & Modeling, Day 2: Core/Database & Backend, Day 3: Integration & Bonus, Day 4: Deployment & Defense).

Deliverable Format Requirements:

Provide complete mermaid code inside a copy-pasteable Markdown code block.

Include a brief explanatory legend summarizing sequential (blocking) vs. parallel (time-optimization) tasks.

Deliver a clearly structured, actionable, and professional response.

## PROMPT FOR DESIGN

Act as a Lead UI/UX Designer and Product Designer expert in Figma, specializing in web administration and management systems (SaaS).

I need you to build an interface prototype in Figma for the "Maintenance and Ticket Control" system for the Fractal technical challenge.

PROJECT CONTEXT & REFERENCES
Based on Fractal's reference prototype: A Kanban board featuring status columns ("Pending", "In Progress", "Closed/Resolved") and a dedicated creation section/side panel for "New Ticket" containing the previously defined fields.

Challenge UI/UX Requirements (R2.1 + Bonus):

Accessible, clear ticket creation form.

Status board with clean visualization of tickets using cards.

Bonus Features: Operator assignment per ticket, visual filters by status/date range, and visual feedback/toast notification elements for status updates.

Modern, functional, and responsive interface layout.

WHAT YOU MUST GENERATE IN YOUR RESPONSE
Figma Base Design System Guide:

Recommended Color Palette (aligned with the reference mockup's visual identity: orange/yellow brand tones, status indicators in gray/orange/green, neutral backgrounds).

Typography scale, spacing rules (8pt grid using Auto Layout), and visual hierarchy.

Core Components to create first (Ticket Cards, Status Badges, Operator Avatars, Buttons, Form Inputs).

Screen Structure & Frames Layout:

Main Screen (Kanban Board / Dashboard): Detailed layout breakdown (Sidebar / Ticket creation form, Header with search/filters, Kanban board with status columns).

Ticket Detail Modal or Side Panel (Transition & History): Interface design for inspecting ticket details, triggering status transitions, assigning operators, and displaying the event history/comment log.

Notification / Toast Component (Bonus): Visual feedback component for successful status updates and actions.

Deliverable:

Use Figma features and conventions to construct the requested layout.

If you have any questions before proceeding with the implementation, ask me first.

## PROMPT FOR DEVELOP

You are implementing the functional core of the backend for "MaintenanceBoard", a maintenance ticket system (technical challenge). The solution and projects already exist; work inside them.

STACK AND STRUCTURE

- .NET (use the SDK pinned in global.json), ASP.NET Core Web API with controllers, SQL Server, Dapper + Microsoft.Data.SqlClient.
- Projects: MaintenanceBoard.Domain, .Application, .Infrastructure, .Api, .Tests. Dependencies: Api -> Application + Infrastructure; Infrastructure -> Application -> Domain. Domain references nothing.
- Modular monolith with two modules, organized as folders inside each layer: Tickets and Access (users). Database schemas: tickets and access.
- No MediatR, no CQRS framework, no EF Core, no ASP.NET Core Identity. Plain application services (use cases); repository interfaces defined in Application and implemented in Infrastructure.
- Controllers must be thin: no business rules in controllers.

ENGLISH FIRST
All code, comments, identifiers, SQL object names, API docs, error messages and commit messages must be in English. Use Conventional Commits.

DATABASE (apply the attached ERD with these corrections)

- Numbered scripts under /database (001_schema.sql, ...), idempotent where practical. No SSDT project.
- Rename APP_USER.User to UserName (User is a reserved word) with a unique index; rename Password to PasswordHash; rename RegisteredAt to CreatedAtUtc. Singular PascalCase tables: access.AppUser, tickets.Ticket, tickets.TicketAssignment, tickets.TicketHistory.
- Use nvarchar for all text columns. All timestamps are UTC (datetime2).
- Add tickets.TicketStatusTransition (FromStatus, ToStatus, composite PK) seeded with the allowed transitions below. Ticket.Status and the history status columns only accept the 4 valid statuses (CHECK or lookup).
- Name every constraint explicitly (PK*, FK*, CK*, UQ*, IX\_). Index TicketHistory(TicketId, CreatedAtUtc) and TicketAssignment(AssignedUserId).
- TicketHistory is append-only.

DOMAIN RULES
Statuses (code and DB, in English): Pending, InProgress, Closed, Rejected.
Roles: Requester, Operator, Supervisor.
Allowed transitions ONLY:
  Pending -> InProgress
  InProgress -> Closed
  Pending -> Rejected
  InProgress -> Rejected
Everything else is rejected: jumps (Pending -> Closed), backward moves, unknown statuses, same-status transitions, and any transition out of Closed or Rejected (terminal states).
Closing requires a non-empty Cause (stored on the ticket). Rejecting requires a non-empty comment (the reason).
Rejected tickets must not appear on the main board.
Implement an explicit Domain class TicketTransitionPolicy (pure, no I/O, unit-tested) with the transition table and the guard rules. Do not implement the State Pattern.

ROLES AND PERMISSIONS

- Create ticket: Requester, Operator, Supervisor.
- Add comment: any role (a Requester only on tickets they reported; Operator/Supervisor on any ticket).
- Assign ticket: Operator, Supervisor (they may assign themselves). Assignees must be active users with role Operator or Supervisor. Only while the ticket is Pending or InProgress.
- Change status (start, close, reject): Operator, Supervisor, on any ticket. Pending -> InProgress requires at least one assignee.
- Max 3 assignees per ticket. The first assignee needs no justification; the 2nd and 3rd require a non-empty Justification. Assigning the same user twice is rejected.
- Use [Authorize(Roles = ...)] on controllers for role-only rules; ownership rules (Requester commenting only on own tickets) live in the application layer.

PERSISTENCE RULES (stored procedures)
The API's database account has EXECUTE on the write stored procedures and SELECT only on tables; it must NOT have INSERT/UPDATE/DELETE on tables. Provide a script that creates the API login/user/role and grants permissions, taking the password from a script variable (never hardcoded).
Write stored procedures (schema tickets), all with SET XACT_ABORT ON, explicit transaction, TRY/CATCH and THROW with custom error numbers:

- usp_CreateTicket(@Title, @Description, @ReportedByUserId): inserts the ticket as Pending and its initial history event (EventType Created, NewStatus Pending) in ONE transaction. Returns the new TicketId.
- usp_TransitionTicketStatus(@TicketId, @ExpectedStatus, @NewStatus, @ActorUserId, @Comment, @Cause): inside the transaction, re-read the CURRENT status with UPDLOCK, HOLDLOCK; if it differs from @ExpectedStatus, THROW a concurrency error; validate the transition against tickets.TicketStatusTransition; validate the Cause/Comment rules; update Status, Cause and UpdatedAtUtc; insert EXACTLY ONE history event (EventType StatusChanged, PreviousStatus, NewStatus, Comment). If the history insert fails, the status change must roll back.
- usp_AssignTicket(@TicketId, @AssignedUserId, @ActorUserId, @Justification): validates ticket state, assignee role/active, the max of 3 (counted inside the transaction under lock), the justification rule and duplicates; inserts the assignment and one history event (EventType Assigned, RelatedUserId = assignee).
- usp_AddTicketComment(@TicketId, @ActorUserId, @Comment): inserts a history event (EventType Comment) without changing the status.
  Custom error numbers (map each to an application exception and an HTTP status):
    50001 ticket not found (404)
    50002 invalid transition (409)
    50003 concurrent modification: current status differs from expected (409)
    50004 cause required to close (422)
    50005 comment required to reject (422)
    50006 assignee limit reached (409)
    50007 justification required (422)
    50008 invalid assignee: not found, inactive or role not allowed (422)
    50009 ticket not open for assignment, or duplicate assignment (409)
  Application layer: before calling a SP, load the ticket, run TicketTransitionPolicy and fail fast with a descriptive error. The SP remains the final guard.
  Reads (Dapper, parameterized, never string concatenation): board (Pending, InProgress, Closed; Rejected excluded), ticket list with optional filters status/from/to (may include Rejected), ticket detail with assignees and full history ordered by CreatedAtUtc.

AUTHENTICATION (simulated, documented limitation)

- Custom AuthenticationHandler that reads the X-User-Id header, loads the active user from the database and builds a ClaimsPrincipal with NameIdentifier and Role claims. Missing/invalid/inactive user -> 401. Role not allowed -> 403.
- Actor ids (ActorUserId, ReportedByUserId, AssignedByUserId) ALWAYS come from the authenticated principal, never from the request body.
- Access module: POST /api/auth/login (userName, password) verifies the password hash and returns userId, names and role (the frontend then sends X-User-Id on every request). GET /api/users (Operator, Supervisor) lists active users for assignee selection.
- Hash passwords in Infrastructure with PasswordHasher from Microsoft.Extensions.Identity.Core (no EF dependency). Because hashes cannot be written by hand in SQL, add a development-only seeder (enabled by an environment variable) that creates one Requester, two Operators and one Supervisor, with seed passwords read from configuration.
- Document in code and README: "Simulated authentication; replace with JWT/OIDC in production. Only the authentication handler changes."

API

- Endpoints: POST /api/tickets, GET /api/tickets/board, GET /api/tickets, GET /api/tickets/{id}, POST /api/tickets/{id}/transitions, POST /api/tickets/{id}/assignments, POST /api/tickets/{id}/comments, POST /api/auth/login, GET /api/users.
- Global exception-handling middleware returning RFC 7807 ProblemDetails with correct status codes. Never a generic 500 for invalid input or rule violations. Model validation errors -> 400 with details.
- OpenAPI/Swagger enabled in development, with the X-User-Id header configurable.
- CORS origin configurable through an environment variable (for the Angular app).
- Configuration only through environment variables / appsettings placeholders. NEVER hardcode connection strings or secrets. Provide a .env.example and a Dockerfile for the API.

TESTS (xUnit)

- Unit tests for TicketTransitionPolicy: every valid transition and every invalid one (jumps, backward, same status, from terminal states, unknown status), plus the Cause/Comment guards.
- Unit tests for application services with fake repositories (role rules, assignment rules).

WORKFLOW
Work in small milestones, one commit per milestone, in this order, and stop after each with a short summary:
  1. Database scripts (schema, transition seed, stored procedures).
  2. Domain + TicketTransitionPolicy + unit tests.
  3. Application layer (use cases, interfaces, exceptions).
  4. Infrastructure (Dapper repositories, SP calls, SqlException-to-exception mapping, password hasher, dev seeder).
  5. Api (auth handler, controllers, middleware, Swagger, Dockerfile, .env.example).
Out of scope: frontend, notifications, unassigning, user CRUD. If a rule is ambiguous, choose the simplest interpretation, note it in a code comment or in the milestone summary, and continue.
If you have a question, ask me before any implementation.
