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

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO [tickets].[TicketStatusTransition] ([FromStatus], [ToStatus])
    SELECT [Allowed].[FromStatus], [Allowed].[ToStatus]
    FROM (VALUES
        (N'Pending', N'InProgress'),
        (N'InProgress', N'Closed'),
        (N'Pending', N'Rejected'),
        (N'InProgress', N'Rejected')
    ) AS [Allowed] ([FromStatus], [ToStatus])
    WHERE NOT EXISTS
    (
        SELECT 1 FROM [tickets].[TicketStatusTransition] WITH (UPDLOCK, HOLDLOCK)
        WHERE [FromStatus] = [Allowed].[FromStatus] AND [ToStatus] = [Allowed].[ToStatus]
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
