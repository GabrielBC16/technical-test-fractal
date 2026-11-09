-- Run only in an isolated validation database as its deployment owner.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'MaintenanceBoard[_]Validation%'
    THROW 51000, N'Use an isolated database named MaintenanceBoard_Validation followed by a suffix.', 1;
GO

CREATE PROCEDURE #AssertError
    @Statement nvarchar(max),
    @ExpectedError int,
    @TicketId int = NULL,
    @ActorUserId int = NULL,
    @AssignedUserId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        EXEC sys.sp_executesql @Statement,
            N'@TicketId int, @ActorUserId int, @AssignedUserId int',
            @TicketId, @ActorUserId, @AssignedUserId;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() = @ExpectedError RETURN;
        DECLARE @Message nvarchar(2048) = CONCAT(N'Expected SQL error ', @ExpectedError, N'; received ', ERROR_NUMBER(), N': ', ERROR_MESSAGE());
        THROW 51000, @Message, 1;
    END CATCH;
    THROW 51000, N'The statement unexpectedly succeeded.', 1;
END;
GO

DECLARE @RunId nvarchar(36) = CONVERT(nvarchar(36), NEWID());
DECLARE @Users TABLE ([UserId] int, [Names] nvarchar(100));
INSERT INTO [access].[AppUser] ([UserName], [PasswordHash], [Names], [LastNames], [Role], [IsActive])
OUTPUT inserted.[UserId], inserted.[Names] INTO @Users
VALUES
    (N'validation-requester-' + @RunId, N'ValidationOnly_NotAnAuthenticationHash', N'Requester', N'Validation', N'Requester', 1),
    (N'validation-other-' + @RunId, N'ValidationOnly_NotAnAuthenticationHash', N'OtherRequester', N'Validation', N'Requester', 1),
    (N'validation-op1-' + @RunId, N'ValidationOnly_NotAnAuthenticationHash', N'Operator1', N'Validation', N'Operator', 1),
    (N'validation-op2-' + @RunId, N'ValidationOnly_NotAnAuthenticationHash', N'Operator2', N'Validation', N'Operator', 1),
    (N'validation-op3-' + @RunId, N'ValidationOnly_NotAnAuthenticationHash', N'Operator3', N'Validation', N'Operator', 1),
    (N'validation-supervisor-' + @RunId, N'ValidationOnly_NotAnAuthenticationHash', N'Supervisor', N'Validation', N'Supervisor', 1),
    (N'validation-inactive-' + @RunId, N'ValidationOnly_NotAnAuthenticationHash', N'Inactive', N'Validation', N'Operator', 0);

DECLARE @Requester int = (SELECT [UserId] FROM @Users WHERE [Names] = N'Requester');
DECLARE @OtherRequester int = (SELECT [UserId] FROM @Users WHERE [Names] = N'OtherRequester');
DECLARE @Operator1 int = (SELECT [UserId] FROM @Users WHERE [Names] = N'Operator1');
DECLARE @Operator2 int = (SELECT [UserId] FROM @Users WHERE [Names] = N'Operator2');
DECLARE @Operator3 int = (SELECT [UserId] FROM @Users WHERE [Names] = N'Operator3');
DECLARE @Supervisor int = (SELECT [UserId] FROM @Users WHERE [Names] = N'Supervisor');
DECLARE @Inactive int = (SELECT [UserId] FROM @Users WHERE [Names] = N'Inactive');
DECLARE @Created TABLE ([TicketId] int);
INSERT INTO @Created EXEC [tickets].[usp_CreateTicket] N'Validation ticket', N'A validation-only maintenance issue.', @Requester;
DECLARE @TicketId int = (SELECT [TicketId] FROM @Created);

IF (SELECT COUNT(*) FROM [tickets].[TicketHistory] WHERE [TicketId] = @TicketId AND [EventType] = N'Created' AND [NewStatus] = N'Pending') <> 1
    THROW 51000, N'Creation must generate exactly one Pending event.', 1;
IF EXISTS (SELECT 1 FROM [tickets].[TicketAssignment] WHERE [TicketId] = @TicketId)
    THROW 51000, N'The reporter must not be automatically assigned.', 1;
IF (SELECT COUNT(*) FROM [tickets].[TicketStatusTransition]) <> 4
    THROW 51000, N'The transition table must contain exactly four pairs.', 1;

EXEC #AssertError N'EXEC tickets.usp_CreateTicket N'' '', N''Description'', @ActorUserId;', 50012, @ActorUserId = @Requester;
EXEC #AssertError N'DECLARE @Title nvarchar(max) = REPLICATE(N''X'', 201); EXEC tickets.usp_CreateTicket @Title, N''Description'', @ActorUserId;', 50012, @ActorUserId = @Requester;
EXEC #AssertError N'EXEC tickets.usp_CreateTicket N''Title'', N''Description'', @ActorUserId;', 50010, @ActorUserId = @Inactive;
EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus -1, N''Pending'', N''InProgress'', @ActorUserId;', 50001, @ActorUserId = @Operator1;
EXEC #AssertError N'EXEC tickets.usp_AssignTicket -1, @AssignedUserId, @ActorUserId;', 50001, @ActorUserId = @Operator1, @AssignedUserId = @Operator1;
EXEC #AssertError N'EXEC tickets.usp_AddTicketComment -1, @ActorUserId, N''Comment'';', 50001, @ActorUserId = @Requester;

EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus @TicketId, N''Pending'', N''Closed'', @ActorUserId, @Cause=N''Cause'';', 50002, @TicketId, @Operator1;
EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus @TicketId, N''Pending'', N''Pending'', @ActorUserId;', 50002, @TicketId, @Operator1;
EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus @TicketId, N''Pending'', N''Unknown'', @ActorUserId;', 50002, @TicketId, @Operator1;
EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus @TicketId, N''Pending'', N''inprogress'', @ActorUserId;', 50002, @TicketId, @Operator1;
EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus @TicketId, N''Pending'', N''InProgress '', @ActorUserId;', 50002, @TicketId, @Operator1;
EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus @TicketId, N''Pending'', N''InProgress'', @ActorUserId;', 50002, @TicketId, @Operator1;
EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus @TicketId, N''Pending'', N''Rejected'', @ActorUserId, N''Reason'';', 50011, @TicketId, @Requester;
EXEC #AssertError N'EXEC tickets.usp_AssignTicket @TicketId, @AssignedUserId, @ActorUserId;', 50011, @TicketId, @Requester, @Operator1;
EXEC #AssertError N'EXEC tickets.usp_AssignTicket @TicketId, @AssignedUserId, @ActorUserId;', 50008, @TicketId, @Operator1, @Requester;
EXEC #AssertError N'EXEC tickets.usp_AssignTicket @TicketId, @AssignedUserId, @ActorUserId;', 50008, @TicketId, @Operator1, @Inactive;
EXEC #AssertError N'EXEC tickets.usp_AssignTicket @TicketId, -1, @ActorUserId;', 50008, @TicketId, @Operator1;

EXEC [tickets].[usp_AssignTicket] @TicketId, @Operator1, @Operator1;
EXEC #AssertError N'EXEC tickets.usp_AssignTicket @TicketId, @AssignedUserId, @ActorUserId;', 50009, @TicketId, @Operator1, @Operator1;
EXEC #AssertError N'EXEC tickets.usp_AssignTicket @TicketId, @AssignedUserId, @ActorUserId;', 50007, @TicketId, @Operator1, @Operator2;
EXEC #AssertError N'DECLARE @Blank nvarchar(10)=NCHAR(9)+NCHAR(160); EXEC tickets.usp_AssignTicket @TicketId, @AssignedUserId, @ActorUserId, @Blank;', 50007, @TicketId, @Operator1, @Operator2;
EXEC [tickets].[usp_AssignTicket] @TicketId, @Operator2, @Supervisor, N'Electrical and hardware review.';
EXEC [tickets].[usp_AssignTicket] @TicketId, @Supervisor, @Supervisor, N'Supervision of the repair.';
EXEC #AssertError N'EXEC tickets.usp_AssignTicket @TicketId, @AssignedUserId, @ActorUserId, N''Additional review'';', 50006, @TicketId, @Operator1, @Operator3;
IF (SELECT COUNT(*) FROM [tickets].[TicketAssignment] WHERE [TicketId] = @TicketId) <> 3
    THROW 51000, N'Exactly three unique assignments must be present.', 1;
IF (SELECT COUNT(*) FROM [tickets].[TicketHistory] WHERE [TicketId] = @TicketId AND [EventType] = N'Assigned') <> 3
    THROW 51000, N'Every assignment must create exactly one history event.', 1;

EXEC [tickets].[usp_TransitionTicketStatus] @TicketId, N'Pending', N'InProgress', @Operator1;
IF (SELECT COUNT(*) FROM [tickets].[TicketHistory] WHERE [TicketId] = @TicketId AND [EventType] = N'StatusChanged') <> 1
    THROW 51000, N'A successful transition must append exactly one event.', 1;
EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus @TicketId, N''Pending'', N''Closed'', @ActorUserId, @Cause=N''Cause'';', 50003, @TicketId, @Operator1;
EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus @TicketId, N''InProgress'', N''Pending'', @ActorUserId;', 50002, @TicketId, @Operator1;
EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus @TicketId, N''InProgress'', N''InProgress'', @ActorUserId;', 50002, @TicketId, @Operator1;
EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus @TicketId, N''InProgress'', N''Closed'', @ActorUserId;', 50004, @TicketId, @Operator1;
EXEC #AssertError N'DECLARE @Blank nvarchar(10)=NCHAR(9)+NCHAR(8239); EXEC tickets.usp_TransitionTicketStatus @TicketId, N''InProgress'', N''Closed'', @ActorUserId, @Cause=@Blank;', 50004, @TicketId, @Operator1;
EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus @TicketId, N''InProgress'', N''Rejected'', @ActorUserId;', 50005, @TicketId, @Operator1;
EXEC #AssertError N'EXEC tickets.usp_AddTicketComment @TicketId, @ActorUserId, N'' '';', 50013, @TicketId, @Requester;
EXEC #AssertError N'EXEC tickets.usp_AddTicketComment @TicketId, @ActorUserId, N''Comment'';', 50011, @TicketId, @OtherRequester;
EXEC [tickets].[usp_AddTicketComment] @TicketId, @Requester, N'The equipment is still offline.';

DELETE FROM @Created;
INSERT INTO @Created EXEC [tickets].[usp_CreateTicket] N'Assignment rollback validation', N'An isolated atomicity fixture.', @Requester;
DECLARE @RollbackTicketId int = (SELECT [TicketId] FROM @Created);

-- Simulate history storage failure after the ticket update, not merely a guard failure.
EXEC(N'CREATE OR ALTER TRIGGER tickets.trg_Validation_FailHistoryInsert ON tickets.TicketHistory
    AFTER INSERT AS BEGIN SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted WHERE [Comment] = N''ValidationForcedHistoryFailure'')
       OR EXISTS (SELECT 1 FROM inserted AS h INNER JOIN tickets.Ticket AS t ON t.TicketId = h.TicketId
                  WHERE h.EventType = N''Created'' AND t.Title = N''ValidationForcedCreationFailure'')
        THROW 51090, N''Simulated history storage failure.'', 1; END;');

DECLARE @HistoryBefore int = (SELECT COUNT(*) FROM [tickets].[TicketHistory] WHERE [TicketId] = @TicketId);
EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus @TicketId, N''InProgress'', N''Closed'', @ActorUserId, N''ValidationForcedHistoryFailure'', N''Power supply failure'';', 51090, @TicketId, @Operator1;
IF NOT EXISTS (SELECT 1 FROM [tickets].[Ticket] WHERE [TicketId] = @TicketId AND [Status] = N'InProgress' AND [Cause] IS NULL)
    THROW 51000, N'History insertion failure must roll back the status and cause.', 1;
IF (SELECT COUNT(*) FROM [tickets].[TicketHistory] WHERE [TicketId] = @TicketId) <> @HistoryBefore
    THROW 51000, N'History insertion failure must not leave a partial event.', 1;
EXEC #AssertError N'EXEC tickets.usp_AssignTicket @TicketId, @AssignedUserId, @ActorUserId, N''ValidationForcedHistoryFailure'';', 51090, @RollbackTicketId, @Operator1, @Operator1;
IF EXISTS (SELECT 1 FROM [tickets].[TicketAssignment] WHERE [TicketId] = @RollbackTicketId)
    THROW 51000, N'History insertion failure must roll back the assignment.', 1;
EXEC #AssertError N'EXEC tickets.usp_AddTicketComment @TicketId, @ActorUserId, N''ValidationForcedHistoryFailure'';', 51090, @RollbackTicketId, @Requester;
IF (SELECT COUNT(*) FROM [tickets].[TicketHistory] WHERE [TicketId] = @RollbackTicketId) <> 1
    THROW 51000, N'Failed assignment and comment writes must not leave history events.', 1;
EXEC #AssertError N'EXEC tickets.usp_CreateTicket N''ValidationForcedCreationFailure'', N''Description'', @ActorUserId;', 51090, @ActorUserId = @Requester;
IF EXISTS (SELECT 1 FROM [tickets].[Ticket] WHERE [Title] = N'ValidationForcedCreationFailure')
    THROW 51000, N'History insertion failure must roll back ticket creation.', 1;
DROP TRIGGER [tickets].[trg_Validation_FailHistoryInsert];

EXEC [tickets].[usp_TransitionTicketStatus] @TicketId, N'InProgress', N'Closed', @Supervisor, @Cause = N'Power supply failure';
IF NOT EXISTS (SELECT 1 FROM [tickets].[Ticket] WHERE [TicketId] = @TicketId AND [Status] = N'Closed' AND [Cause] = N'Power supply failure')
    THROW 51000, N'Closure must persist the cause.', 1;
EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus @TicketId, N''Closed'', N''InProgress'', @ActorUserId;', 50002, @TicketId, @Operator1;
EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus @TicketId, N''Closed'', N''Rejected'', @ActorUserId, N''Reason'';', 50002, @TicketId, @Operator1;
EXEC #AssertError N'EXEC tickets.usp_AssignTicket @TicketId, @AssignedUserId, @ActorUserId;', 50009, @TicketId, @Supervisor, @Operator3;
EXEC [tickets].[usp_AddTicketComment] @TicketId, @Requester, N'Closure acknowledged.';
EXEC #AssertError N'UPDATE tickets.TicketHistory SET [Comment]=N''Edited'' WHERE [TicketId]=@TicketId;', 50014, @TicketId;
EXEC #AssertError N'DELETE FROM tickets.TicketHistory WHERE [TicketId]=@TicketId;', 50014, @TicketId;
EXEC #AssertError N'INSERT INTO tickets.TicketHistory (TicketId, ActorUserId, EventType, Comment) VALUES (-1, @ActorUserId, N''Comment'', N''Orphan'');', 547, @ActorUserId = @Operator1;

DELETE FROM @Created;
INSERT INTO @Created EXEC [tickets].[usp_CreateTicket] N'Pending rejection', N'Duplicate incident.', @Requester;
DECLARE @RejectedId int = (SELECT [TicketId] FROM @Created);
EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus @TicketId, N''Pending'', N''Rejected'', @ActorUserId;', 50005, @RejectedId, @Operator1;
EXEC [tickets].[usp_TransitionTicketStatus] @RejectedId, N'Pending', N'Rejected', @Operator1, N'Duplicate of an existing incident.';
EXEC #AssertError N'EXEC tickets.usp_TransitionTicketStatus @TicketId, N''Rejected'', N''Pending'', @ActorUserId;', 50002, @RejectedId, @Operator1;
IF NOT EXISTS (SELECT 1 FROM [tickets].[TicketHistory] WHERE [TicketId] = @RejectedId AND [NewStatus] = N'Rejected' AND [Comment] = N'Duplicate of an existing incident.')
    THROW 51000, N'Rejection must retain its reason.', 1;

DELETE FROM @Created;
INSERT INTO @Created EXEC [tickets].[usp_CreateTicket] N'In-progress rejection', N'Duplicate discovered during attention.', @Supervisor;
DECLARE @InProgressRejectedId int = (SELECT [TicketId] FROM @Created);
EXEC [tickets].[usp_AssignTicket] @InProgressRejectedId, @Supervisor, @Supervisor;
EXEC [tickets].[usp_TransitionTicketStatus] @InProgressRejectedId, N'Pending', N'InProgress', @Supervisor;
EXEC [tickets].[usp_TransitionTicketStatus] @InProgressRejectedId, N'InProgress', N'Rejected', @Supervisor, N'Already being handled elsewhere.';

-- Confirm effective runtime permissions and ownership chaining without a SQL login.
IF DATABASE_PRINCIPAL_ID(N'MaintenanceBoardApiRole') IS NOT NULL
BEGIN
    DECLARE @RuntimeUser sysname = N'ValidationRuntime_' + REPLACE(@RunId, N'-', N'');
    DECLARE @PermissionCheck nvarchar(max) = N'CREATE USER ' + QUOTENAME(@RuntimeUser) + N' WITHOUT LOGIN;
        ALTER ROLE [MaintenanceBoardApiRole] ADD MEMBER ' + QUOTENAME(@RuntimeUser) + N';
        EXECUTE AS USER = N''' + @RuntimeUser + N''';
        BEGIN TRY
            IF HAS_PERMS_BY_NAME(N''tickets.Ticket'', N''OBJECT'', N''SELECT'') <> 1
                THROW 51000, N''Runtime SELECT permission is missing.'', 1;
            IF HAS_PERMS_BY_NAME(N''tickets.Ticket'', N''OBJECT'', N''UPDATE'') <> 0
               OR HAS_PERMS_BY_NAME(N''tickets.Ticket'', N''OBJECT'', N''INSERT'') <> 0
               OR HAS_PERMS_BY_NAME(N''tickets.TicketHistory'', N''OBJECT'', N''DELETE'') <> 0
                THROW 51000, N''Runtime direct table writes must be denied.'', 1;
            EXEC tickets.usp_AddTicketComment @TicketId, @ActorUserId, N''Ownership chaining validated.'';
            REVERT;
        END TRY
        BEGIN CATCH
            REVERT;
            THROW;
        END CATCH;
        DROP USER ' + QUOTENAME(@RuntimeUser) + N';';
    EXEC sys.sp_executesql @PermissionCheck, N'@TicketId int, @ActorUserId int', @TicketId, @Operator1;
END;

PRINT N'PASS: workflow guards, all four valid transitions, atomic rollback, history integrity, and available runtime permissions.';
GO
