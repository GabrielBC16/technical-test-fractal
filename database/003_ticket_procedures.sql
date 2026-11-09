SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO

-- Match the Unicode whitespace characters recognized by the application guards.
CREATE OR ALTER FUNCTION [tickets].[ufn_HasText] (@Value nvarchar(max))
RETURNS bit
AS
BEGIN
    DECLARE @Whitespace nvarchar(32) =
        NCHAR(9) + NCHAR(10) + NCHAR(11) + NCHAR(12) + NCHAR(13) + NCHAR(133) +
        NCHAR(160) + NCHAR(5760) + NCHAR(8192) + NCHAR(8193) + NCHAR(8194) +
        NCHAR(8195) + NCHAR(8196) + NCHAR(8197) + NCHAR(8198) + NCHAR(8199) +
        NCHAR(8200) + NCHAR(8201) + NCHAR(8202) + NCHAR(8232) + NCHAR(8233) +
        NCHAR(8239) + NCHAR(8287) + NCHAR(12288);

    -- Binary collation prevents linguistic equivalence from skipping Unicode characters.
    RETURN CASE WHEN LEN(LTRIM(RTRIM(TRANSLATE(
        @Value COLLATE Latin1_General_100_BIN2,
        @Whitespace COLLATE Latin1_General_100_BIN2,
        REPLICATE(N' ', DATALENGTH(@Whitespace) / 2))))) > 0
        THEN 1 ELSE 0 END;
END;
GO

CREATE OR ALTER PROCEDURE [tickets].[usp_CreateTicket]
    @Title nvarchar(max),
    @Description nvarchar(max),
    @ReportedByUserId int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET ANSI_WARNINGS ON;
    SET ARITHABORT ON;
    SET NUMERIC_ROUNDABORT OFF;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS
        (
            SELECT 1 FROM [access].[AppUser] WITH (HOLDLOCK)
            WHERE [UserId] = @ReportedByUserId AND [IsActive] = 1
        )
            THROW 50010, N'The authenticated user was not found or is inactive.', 1;

        -- Wide parameters allow explicit length checks instead of silent truncation.
        IF [tickets].[ufn_HasText](@Title) = 0 OR DATALENGTH(@Title) > 400
            THROW 50012, N'Title is required and must not exceed 200 characters.', 1;
        IF [tickets].[ufn_HasText](@Description) = 0 OR DATALENGTH(@Description) > 8000
            THROW 50012, N'Description is required and must not exceed 4000 characters.', 1;

        DECLARE @Now datetime2(7) = SYSUTCDATETIME();

        INSERT INTO [tickets].[Ticket]
            ([Title], [Description], [Status], [ReportedByUserId], [CreatedAtUtc], [UpdatedAtUtc])
        VALUES
            (LTRIM(RTRIM(@Title)), LTRIM(RTRIM(@Description)), N'Pending', @ReportedByUserId, @Now, @Now);

        DECLARE @TicketId int = CONVERT(int, SCOPE_IDENTITY());

        INSERT INTO [tickets].[TicketHistory]
            ([TicketId], [ActorUserId], [EventType], [NewStatus], [CreatedAtUtc])
        VALUES
            (@TicketId, @ReportedByUserId, N'Created', N'Pending', @Now);

        COMMIT TRANSACTION;

        SELECT @TicketId AS [TicketId];
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE [tickets].[usp_TransitionTicketStatus]
    @TicketId int,
    @ExpectedStatus nvarchar(max),
    @NewStatus nvarchar(max),
    @ActorUserId int,
    @Comment nvarchar(max) = NULL,
    @Cause nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET ANSI_WARNINGS ON;
    SET ARITHABORT ON;
    SET NUMERIC_ROUNDABORT OFF;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @CurrentStatus nvarchar(20);
        -- Assignment and comment procedures lock this same parent first.
        -- This serializes workflow writes, including under READ_COMMITTED_SNAPSHOT.
        SELECT @CurrentStatus = [Status]
        FROM [tickets].[Ticket] WITH (UPDLOCK, HOLDLOCK)
        WHERE [TicketId] = @TicketId;

        IF @CurrentStatus IS NULL
            THROW 50001, N'The ticket was not found.', 1;

        DECLARE @ActorRole nvarchar(20);
        SELECT @ActorRole = [Role] FROM [access].[AppUser] WITH (HOLDLOCK)
        WHERE [UserId] = @ActorUserId AND [IsActive] = 1;

        IF @ActorRole IS NULL
            THROW 50010, N'The authenticated user was not found or is inactive.', 1;
        IF @ActorRole NOT IN (N'Operator', N'Supervisor')
            THROW 50011, N'Only operators and supervisors can change ticket status.', 1;

        IF @ExpectedStatus IS NULL
           OR @CurrentStatus <> @ExpectedStatus COLLATE Latin1_General_100_BIN2
           OR DATALENGTH(@CurrentStatus) <> DATALENGTH(@ExpectedStatus)
            THROW 50003, N'The ticket status changed. Reload the ticket before retrying.', 1;

        IF NOT EXISTS
        (
            SELECT 1 FROM [tickets].[TicketStatusTransition] WITH (HOLDLOCK)
            WHERE [FromStatus] = @CurrentStatus
              AND [ToStatus] = @NewStatus COLLATE Latin1_General_100_BIN2
              AND DATALENGTH([ToStatus]) = DATALENGTH(@NewStatus)
        )
            THROW 50002, N'The requested ticket status transition is not allowed.', 1;

        IF @NewStatus = N'InProgress' AND NOT EXISTS
        (
            SELECT 1 FROM [tickets].[TicketAssignment] WITH (UPDLOCK, HOLDLOCK)
            WHERE [TicketId] = @TicketId
        )
            THROW 50002, N'Assign at least one operator before starting ticket attention.', 1;

        IF @NewStatus = N'Closed' AND [tickets].[ufn_HasText](@Cause) = 0
            THROW 50004, N'A non-empty cause is required to close the ticket.', 1;
        IF @NewStatus = N'Rejected' AND [tickets].[ufn_HasText](@Comment) = 0
            THROW 50005, N'A non-empty rejection reason is required.', 1;
        IF DATALENGTH(@Cause) > 8000 OR DATALENGTH(@Comment) > 8000
            THROW 50012, N'Cause and comment must not exceed 4000 characters.', 1;

        DECLARE @Now datetime2(7) = SYSUTCDATETIME();

        -- Cause describes the root cause and is stored when closing, not when rejecting.
        UPDATE [tickets].[Ticket]
        SET [Status] = @NewStatus,
            [Cause] = CASE WHEN @NewStatus = N'Closed' THEN LTRIM(RTRIM(@Cause)) ELSE [Cause] END,
            [UpdatedAtUtc] = @Now
        WHERE [TicketId] = @TicketId;

        INSERT INTO [tickets].[TicketHistory]
            ([TicketId], [ActorUserId], [EventType], [PreviousStatus], [NewStatus], [Comment], [CreatedAtUtc])
        VALUES
            (@TicketId, @ActorUserId, N'StatusChanged', @CurrentStatus, @NewStatus,
             CASE WHEN [tickets].[ufn_HasText](@Comment) = 1 THEN LTRIM(RTRIM(@Comment)) ELSE NULL END, @Now);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE [tickets].[usp_AssignTicket]
    @TicketId int,
    @AssignedUserId int,
    @ActorUserId int,
    @Justification nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET ANSI_WARNINGS ON;
    SET ARITHABORT ON;
    SET NUMERIC_ROUNDABORT OFF;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @CurrentStatus nvarchar(20);
        SELECT @CurrentStatus = [Status]
        FROM [tickets].[Ticket] WITH (UPDLOCK, HOLDLOCK)
        WHERE [TicketId] = @TicketId;

        IF @CurrentStatus IS NULL
            THROW 50001, N'The ticket was not found.', 1;

        DECLARE @ActorRole nvarchar(20);
        SELECT @ActorRole = [Role] FROM [access].[AppUser] WITH (HOLDLOCK)
        WHERE [UserId] = @ActorUserId AND [IsActive] = 1;

        IF @ActorRole IS NULL
            THROW 50010, N'The authenticated user was not found or is inactive.', 1;
        IF @ActorRole NOT IN (N'Operator', N'Supervisor')
            THROW 50011, N'Only operators and supervisors can assign tickets.', 1;
        IF @CurrentStatus NOT IN (N'Pending', N'InProgress')
            THROW 50009, N'Only pending or in-progress tickets can be assigned.', 1;

        IF NOT EXISTS
        (
            SELECT 1 FROM [access].[AppUser] WITH (HOLDLOCK)
            WHERE [UserId] = @AssignedUserId AND [IsActive] = 1 AND [Role] IN (N'Operator', N'Supervisor')
        )
            THROW 50008, N'The assignee must be an active operator or supervisor.', 1;

        IF EXISTS
        (
            SELECT 1 FROM [tickets].[TicketAssignment] WITH (UPDLOCK, HOLDLOCK)
            WHERE [TicketId] = @TicketId AND [AssignedUserId] = @AssignedUserId
        )
            THROW 50009, N'The user is already assigned to this ticket.', 1;

        DECLARE @AssignmentCount int;
        SELECT @AssignmentCount = COUNT(*)
        FROM [tickets].[TicketAssignment] WITH (UPDLOCK, HOLDLOCK)
        WHERE [TicketId] = @TicketId;

        IF @AssignmentCount >= 3
            THROW 50006, N'A ticket cannot have more than three assignees.', 1;
        IF @AssignmentCount >= 1 AND [tickets].[ufn_HasText](@Justification) = 0
            THROW 50007, N'A justification is required for additional assignees.', 1;
        IF DATALENGTH(@Justification) > 4000
            THROW 50012, N'Assignment justification must not exceed 2000 characters.', 1;

        DECLARE @Now datetime2(7) = SYSUTCDATETIME();
        DECLARE @NormalizedJustification nvarchar(2000) =
            CASE WHEN [tickets].[ufn_HasText](@Justification) = 1 THEN LTRIM(RTRIM(@Justification)) ELSE NULL END;

        INSERT INTO [tickets].[TicketAssignment]
            ([TicketId], [AssignedUserId], [AssignedByUserId], [Justification], [AssignedAtUtc])
        VALUES
            (@TicketId, @AssignedUserId, @ActorUserId, @NormalizedJustification, @Now);

        INSERT INTO [tickets].[TicketHistory]
            ([TicketId], [ActorUserId], [RelatedUserId], [EventType], [Comment], [CreatedAtUtc])
        VALUES
            (@TicketId, @ActorUserId, @AssignedUserId, N'Assigned', @NormalizedJustification, @Now);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE [tickets].[usp_AddTicketComment]
    @TicketId int,
    @ActorUserId int,
    @Comment nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET ANSI_WARNINGS ON;
    SET ARITHABORT ON;
    SET NUMERIC_ROUNDABORT OFF;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @ReportedByUserId int;
        SELECT @ReportedByUserId = [ReportedByUserId]
        FROM [tickets].[Ticket] WITH (UPDLOCK, HOLDLOCK)
        WHERE [TicketId] = @TicketId;

        IF @ReportedByUserId IS NULL
            THROW 50001, N'The ticket was not found.', 1;

        DECLARE @ActorRole nvarchar(20);
        SELECT @ActorRole = [Role] FROM [access].[AppUser] WITH (HOLDLOCK)
        WHERE [UserId] = @ActorUserId AND [IsActive] = 1;

        IF @ActorRole IS NULL
            THROW 50010, N'The authenticated user was not found or is inactive.', 1;
        IF @ActorRole = N'Requester' AND @ReportedByUserId <> @ActorUserId
            THROW 50011, N'Requesters can comment only on tickets they reported.', 1;
        IF [tickets].[ufn_HasText](@Comment) = 0
            THROW 50013, N'A non-empty comment is required.', 1;
        IF DATALENGTH(@Comment) > 8000
            THROW 50012, N'Comment must not exceed 4000 characters.', 1;

        -- Comments remain allowed on terminal tickets and never change ticket status.
        INSERT INTO [tickets].[TicketHistory]
            ([TicketId], [ActorUserId], [EventType], [Comment], [CreatedAtUtc])
        VALUES
            (@TicketId, @ActorUserId, N'Comment', LTRIM(RTRIM(@Comment)), SYSUTCDATETIME());

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
