-- Run against an existing, empty application database as its deployment owner.
-- This bootstrap script preserves existing tables; it is not a schema migration engine.

CREATE DATABASE MaintenanceBoard;
USE MaintenanceBoard;
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

IF SCHEMA_ID(N'access') IS NULL
    EXEC(N'CREATE SCHEMA [access] AUTHORIZATION [dbo];');
IF SCHEMA_ID(N'tickets') IS NULL
    EXEC(N'CREATE SCHEMA [tickets] AUTHORIZATION [dbo];');
GO

IF OBJECT_ID(N'access.AppUser', N'U') IS NULL
BEGIN
    CREATE TABLE [access].[AppUser]
    (
        [UserId] int IDENTITY(1, 1) NOT NULL,
        [UserName] nvarchar(20) NOT NULL,
        [PasswordHash] nvarchar(255) NOT NULL,
        [Names] nvarchar(100) NOT NULL,
        [LastNames] nvarchar(150) NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_AppUser_IsActive] DEFAULT (1),
        [CreatedAtUtc] datetime2(7) NOT NULL
            CONSTRAINT [DF_AppUser_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_AppUser] PRIMARY KEY ([UserId]),
        CONSTRAINT [CK_AppUser_UserName_NotBlank] CHECK (LEN(LTRIM(RTRIM([UserName]))) > 0),
        CONSTRAINT [CK_AppUser_PasswordHash_NotBlank] CHECK (LEN(LTRIM(RTRIM([PasswordHash]))) > 0),
        CONSTRAINT [CK_AppUser_Names_NotBlank] CHECK (LEN(LTRIM(RTRIM([Names]))) > 0),
        CONSTRAINT [CK_AppUser_LastNames_NotBlank] CHECK (LEN(LTRIM(RTRIM([LastNames]))) > 0),
        CONSTRAINT [CK_AppUser_Role] CHECK
            ([Role] COLLATE Latin1_General_100_BIN2 IN (N'Requester', N'Operator', N'Supervisor')
             AND DATALENGTH([Role]) = DATALENGTH(RTRIM([Role])))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'access.AppUser') AND [name] = N'IX_AppUser_UserName')
    CREATE UNIQUE INDEX [IX_AppUser_UserName] ON [access].[AppUser] ([UserName]);
GO

IF OBJECT_ID(N'tickets.TicketStatusTransition', N'U') IS NULL
BEGIN
    CREATE TABLE [tickets].[TicketStatusTransition]
    (
        [FromStatus] nvarchar(20) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [ToStatus] nvarchar(20) COLLATE Latin1_General_100_BIN2 NOT NULL,
        CONSTRAINT [PK_TicketStatusTransition] PRIMARY KEY ([FromStatus], [ToStatus]),
        CONSTRAINT [CK_TicketStatusTransition_AllowedPair] CHECK
        (
            (([FromStatus] = N'Pending' AND [ToStatus] IN (N'InProgress', N'Rejected'))
             OR ([FromStatus] = N'InProgress' AND [ToStatus] IN (N'Closed', N'Rejected')))
            AND DATALENGTH([FromStatus]) = DATALENGTH(RTRIM([FromStatus]))
            AND DATALENGTH([ToStatus]) = DATALENGTH(RTRIM([ToStatus]))
        )
    );
END;
GO

IF OBJECT_ID(N'tickets.Ticket', N'U') IS NULL
BEGIN
    CREATE TABLE [tickets].[Ticket]
    (
        [TicketId] int IDENTITY(1, 1) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(4000) NOT NULL,
        [Status] nvarchar(20) COLLATE Latin1_General_100_BIN2 NOT NULL
            CONSTRAINT [DF_Ticket_Status] DEFAULT (N'Pending'),
        [Cause] nvarchar(4000) NULL,
        [ReportedByUserId] int NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL
            CONSTRAINT [DF_Ticket_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] datetime2(7) NOT NULL
            CONSTRAINT [DF_Ticket_UpdatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [Version] rowversion NOT NULL,
        CONSTRAINT [PK_Ticket] PRIMARY KEY ([TicketId]),
        CONSTRAINT [FK_Ticket_AppUser_ReportedByUserId] FOREIGN KEY ([ReportedByUserId])
            REFERENCES [access].[AppUser] ([UserId]),
        CONSTRAINT [CK_Ticket_Title_NotBlank] CHECK (LEN(LTRIM(RTRIM([Title]))) > 0),
        CONSTRAINT [CK_Ticket_Description_NotBlank] CHECK (LEN(LTRIM(RTRIM([Description]))) > 0),
        CONSTRAINT [CK_Ticket_Status] CHECK
            ([Status] IN (N'Pending', N'InProgress', N'Closed', N'Rejected')
             AND DATALENGTH([Status]) = DATALENGTH(RTRIM([Status]))),
        CONSTRAINT [CK_Ticket_ClosedCause] CHECK
            ([Status] <> N'Closed' OR ([Cause] IS NOT NULL AND LEN(LTRIM(RTRIM([Cause]))) > 0)),
        CONSTRAINT [CK_Ticket_Timestamps] CHECK ([UpdatedAtUtc] >= [CreatedAtUtc])
    );
END;
GO

IF OBJECT_ID(N'tickets.TicketAssignment', N'U') IS NULL
BEGIN
    CREATE TABLE [tickets].[TicketAssignment]
    (
        [TicketId] int NOT NULL,
        [AssignedUserId] int NOT NULL,
        [AssignedByUserId] int NOT NULL,
        [Justification] nvarchar(2000) NULL,
        [AssignedAtUtc] datetime2(7) NOT NULL
            CONSTRAINT [DF_TicketAssignment_AssignedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_TicketAssignment] PRIMARY KEY ([TicketId], [AssignedUserId]),
        CONSTRAINT [FK_TicketAssignment_Ticket] FOREIGN KEY ([TicketId])
            REFERENCES [tickets].[Ticket] ([TicketId]),
        CONSTRAINT [FK_TicketAssignment_AppUser_AssignedUserId] FOREIGN KEY ([AssignedUserId])
            REFERENCES [access].[AppUser] ([UserId]),
        CONSTRAINT [FK_TicketAssignment_AppUser_AssignedByUserId] FOREIGN KEY ([AssignedByUserId])
            REFERENCES [access].[AppUser] ([UserId]),
        CONSTRAINT [CK_TicketAssignment_Justification_NotBlank] CHECK
            ([Justification] IS NULL OR LEN(LTRIM(RTRIM([Justification]))) > 0)
    );
END;
GO

IF OBJECT_ID(N'tickets.TicketHistory', N'U') IS NULL
BEGIN
    CREATE TABLE [tickets].[TicketHistory]
    (
        [HistoryId] bigint IDENTITY(1, 1) NOT NULL,
        [TicketId] int NOT NULL,
        [ActorUserId] int NOT NULL,
        [RelatedUserId] int NULL,
        [EventType] nvarchar(20) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [PreviousStatus] nvarchar(20) COLLATE Latin1_General_100_BIN2 NULL,
        [NewStatus] nvarchar(20) COLLATE Latin1_General_100_BIN2 NULL,
        [Comment] nvarchar(4000) NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL
            CONSTRAINT [DF_TicketHistory_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_TicketHistory] PRIMARY KEY ([HistoryId]),
        CONSTRAINT [FK_TicketHistory_Ticket] FOREIGN KEY ([TicketId])
            REFERENCES [tickets].[Ticket] ([TicketId]),
        CONSTRAINT [FK_TicketHistory_AppUser_ActorUserId] FOREIGN KEY ([ActorUserId])
            REFERENCES [access].[AppUser] ([UserId]),
        CONSTRAINT [FK_TicketHistory_AppUser_RelatedUserId] FOREIGN KEY ([RelatedUserId])
            REFERENCES [access].[AppUser] ([UserId]),
        CONSTRAINT [CK_TicketHistory_EventType] CHECK
            ([EventType] IN (N'Created', N'StatusChanged', N'Assigned', N'Comment')
             AND DATALENGTH([EventType]) = DATALENGTH(RTRIM([EventType]))),
        CONSTRAINT [CK_TicketHistory_PreviousStatus] CHECK
            ([PreviousStatus] IS NULL OR
             ([PreviousStatus] IN (N'Pending', N'InProgress', N'Closed', N'Rejected')
              AND DATALENGTH([PreviousStatus]) = DATALENGTH(RTRIM([PreviousStatus])))),
        CONSTRAINT [CK_TicketHistory_NewStatus] CHECK
            ([NewStatus] IS NULL OR
             ([NewStatus] IN (N'Pending', N'InProgress', N'Closed', N'Rejected')
              AND DATALENGTH([NewStatus]) = DATALENGTH(RTRIM([NewStatus])))),
        CONSTRAINT [CK_TicketHistory_EventShape] CHECK
        (
            ([EventType] = N'Created' AND [PreviousStatus] IS NULL AND [NewStatus] IS NOT NULL
             AND [NewStatus] = N'Pending' AND [RelatedUserId] IS NULL)
            OR
            ([EventType] = N'StatusChanged' AND [PreviousStatus] IS NOT NULL AND [NewStatus] IS NOT NULL
             AND [PreviousStatus] <> [NewStatus] AND [RelatedUserId] IS NULL
             AND ([NewStatus] <> N'Rejected' OR ([Comment] IS NOT NULL AND LEN(LTRIM(RTRIM([Comment]))) > 0)))
            OR
            ([EventType] = N'Assigned' AND [PreviousStatus] IS NULL AND [NewStatus] IS NULL
             AND [RelatedUserId] IS NOT NULL)
            OR
            ([EventType] = N'Comment' AND [PreviousStatus] IS NULL AND [NewStatus] IS NULL
             AND [RelatedUserId] IS NULL AND [Comment] IS NOT NULL AND LEN(LTRIM(RTRIM([Comment]))) > 0)
        )
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'tickets.Ticket') AND [name] = N'IX_Ticket_Status_CreatedAtUtc')
    CREATE INDEX [IX_Ticket_Status_CreatedAtUtc] ON [tickets].[Ticket] ([Status], [CreatedAtUtc], [TicketId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'tickets.TicketHistory') AND [name] = N'IX_TicketHistory_TicketId_CreatedAtUtc')
    CREATE INDEX [IX_TicketHistory_TicketId_CreatedAtUtc]
        ON [tickets].[TicketHistory] ([TicketId], [CreatedAtUtc], [HistoryId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'tickets.TicketHistory') AND [name] = N'IX_TicketHistory_Created_TicketId')
    CREATE UNIQUE INDEX [IX_TicketHistory_Created_TicketId]
        ON [tickets].[TicketHistory] ([TicketId]) WHERE [EventType] = N'Created';
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [object_id] = OBJECT_ID(N'tickets.TicketAssignment') AND [name] = N'IX_TicketAssignment_AssignedUserId')
    CREATE INDEX [IX_TicketAssignment_AssignedUserId]
        ON [tickets].[TicketAssignment] ([AssignedUserId], [TicketId]);
GO

CREATE OR ALTER TRIGGER [tickets].[trg_TicketHistory_AppendOnly]
ON [tickets].[TicketHistory]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 50014, N'Ticket history is append-only and cannot be updated or deleted.', 1;
END;
GO
