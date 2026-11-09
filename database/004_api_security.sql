-- SQLCMD mode is required. Apply-Database.ps1 supplies UTF-16 hexadecimal variables.
-- Never put a plaintext password in this script or pass it on a command line.
-- Target: SQL Server. Azure SQL Database requires contained-user provisioning instead.
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

DECLARE @LoginBytes varbinary(max) = TRY_CONVERT(varbinary(max), N'$(ApiLoginHex)', 1);
DECLARE @UserBytes varbinary(max) = TRY_CONVERT(varbinary(max), N'$(ApiUserHex)', 1);
DECLARE @PasswordBytes varbinary(max) = TRY_CONVERT(varbinary(max), N'$(ApiPasswordHex)', 1);

IF @LoginBytes IS NULL OR DATALENGTH(@LoginBytes) = 0 OR DATALENGTH(@LoginBytes) > 256 OR DATALENGTH(@LoginBytes) % 2 <> 0
    THROW 50012, N'A valid API login name is required.', 1;
IF @UserBytes IS NULL OR DATALENGTH(@UserBytes) = 0 OR DATALENGTH(@UserBytes) > 256 OR DATALENGTH(@UserBytes) % 2 <> 0
    THROW 50012, N'A valid API database user name is required.', 1;
IF @PasswordBytes IS NULL OR DATALENGTH(@PasswordBytes) > 256 OR DATALENGTH(@PasswordBytes) % 2 <> 0
    THROW 50012, N'A valid API database password is required.', 1;

DECLARE @ApiLogin sysname = CONVERT(nvarchar(128), @LoginBytes);
DECLARE @ApiUser sysname = CONVERT(nvarchar(128), @UserBytes);
DECLARE @ApiPassword nvarchar(128) = CONVERT(nvarchar(128), @PasswordBytes);
DECLARE @RoleName sysname = N'MaintenanceBoardApiRole';

IF LEN(LTRIM(RTRIM(@ApiLogin))) = 0 OR LEN(LTRIM(RTRIM(@ApiUser))) = 0
    THROW 50012, N'API principal names cannot be blank.', 1;
IF LEN(@ApiPassword) < 12
    THROW 50012, N'The API database password must contain at least 12 characters.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF SUSER_ID(@ApiLogin) IS NULL
    BEGIN
        DECLARE @CreateLogin nvarchar(max) =
            N'CREATE LOGIN ' + QUOTENAME(@ApiLogin) + N' WITH PASSWORD = N''' +
            REPLACE(@ApiPassword, N'''', N'''''') + N''', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';
        EXEC sys.sp_executesql @CreateLogin;
    END;

    IF EXISTS (SELECT 1 FROM sys.server_principals WHERE [name] = @ApiLogin AND [type] <> N'S')
        THROW 50015, N'The API login must be a dedicated SQL login.', 1;
    IF EXISTS (SELECT 1 FROM sys.server_principals WHERE [name] = @ApiLogin AND [is_disabled] = 1)
        THROW 50015, N'The API login is disabled.', 1;
    IF EXISTS (SELECT 1 FROM sys.server_role_members WHERE [member_principal_id] = SUSER_ID(@ApiLogin))
        THROW 50015, N'The API login must not belong to a server role.', 1;
    IF EXISTS
    (
        SELECT 1 FROM sys.server_permissions
        WHERE [grantee_principal_id] = SUSER_ID(@ApiLogin) AND [state] IN (N'G', N'W')
          AND [permission_name] <> N'CONNECT SQL'
    )
        THROW 50015, N'The API login has unexpected server permissions.', 1;
    IF EXISTS (SELECT 1 FROM sys.databases WHERE [database_id] = DB_ID() AND [owner_sid] = SUSER_SID(@ApiLogin))
        THROW 50015, N'The API login must not own the database.', 1;

    IF DATABASE_PRINCIPAL_ID(@ApiUser) IS NULL
    BEGIN
        DECLARE @CreateUser nvarchar(max) = N'CREATE USER ' + QUOTENAME(@ApiUser) +
            N' FOR LOGIN ' + QUOTENAME(@ApiLogin) + N' WITH DEFAULT_SCHEMA = [tickets];';
        EXEC sys.sp_executesql @CreateUser;
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.database_principals
        WHERE [name] = @ApiUser AND [sid] = SUSER_SID(@ApiLogin) AND [type] = N'S'
    )
        THROW 50015, N'The API database user is not mapped to the requested SQL login.', 1;

    IF DATABASE_PRINCIPAL_ID(@RoleName) IS NULL
        CREATE ROLE [MaintenanceBoardApiRole] AUTHORIZATION [dbo];

    IF EXISTS
    (
        SELECT 1 FROM sys.database_role_members
        WHERE [member_principal_id] = DATABASE_PRINCIPAL_ID(@RoleName)
    )
        THROW 50015, N'The API role must not inherit another database role.', 1;
    IF EXISTS
    (
        SELECT 1 FROM sys.database_permissions
        WHERE [grantee_principal_id] = DATABASE_PRINCIPAL_ID(@RoleName) AND [state] IN (N'G', N'W')
          AND NOT
          (
              ([class] = 1 AND [minor_id] = 0 AND [permission_name] = N'SELECT' AND [major_id] IN
                  (OBJECT_ID(N'access.AppUser'), OBJECT_ID(N'tickets.Ticket'), OBJECT_ID(N'tickets.TicketAssignment'),
                   OBJECT_ID(N'tickets.TicketHistory'), OBJECT_ID(N'tickets.TicketStatusTransition')))
              OR
              ([class] = 1 AND [minor_id] = 0 AND [permission_name] = N'EXECUTE' AND [major_id] IN
                  (OBJECT_ID(N'tickets.usp_CreateTicket'), OBJECT_ID(N'tickets.usp_TransitionTicketStatus'),
                   OBJECT_ID(N'tickets.usp_AssignTicket'), OBJECT_ID(N'tickets.usp_AddTicketComment')))
          )
    )
        THROW 50015, N'The API role has permissions outside its runtime contract.', 1;

    IF EXISTS
    (
        SELECT 1 FROM sys.database_role_members
        WHERE [member_principal_id] = DATABASE_PRINCIPAL_ID(@ApiUser)
          AND [role_principal_id] <> DATABASE_PRINCIPAL_ID(@RoleName)
    )
        THROW 50015, N'The API database user must not belong to additional database roles.', 1;
    IF EXISTS
    (
        SELECT 1 FROM sys.database_permissions
        WHERE [grantee_principal_id] = DATABASE_PRINCIPAL_ID(@ApiUser)
          AND [state] IN (N'G', N'W')
          AND [permission_name] <> N'CONNECT'
    )
        THROW 50015, N'The API database user has unexpected direct permissions.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.database_role_members
        WHERE [member_principal_id] = DATABASE_PRINCIPAL_ID(@ApiUser)
          AND [role_principal_id] = DATABASE_PRINCIPAL_ID(@RoleName)
    )
    BEGIN
        DECLARE @AddMember nvarchar(max) = N'ALTER ROLE [MaintenanceBoardApiRole] ADD MEMBER ' + QUOTENAME(@ApiUser) + N';';
        EXEC sys.sp_executesql @AddMember;
    END;

    GRANT SELECT ON OBJECT::[access].[AppUser] TO [MaintenanceBoardApiRole];
    GRANT SELECT ON OBJECT::[tickets].[Ticket] TO [MaintenanceBoardApiRole];
    GRANT SELECT ON OBJECT::[tickets].[TicketAssignment] TO [MaintenanceBoardApiRole];
    GRANT SELECT ON OBJECT::[tickets].[TicketHistory] TO [MaintenanceBoardApiRole];
    GRANT SELECT ON OBJECT::[tickets].[TicketStatusTransition] TO [MaintenanceBoardApiRole];

    GRANT EXECUTE ON OBJECT::[tickets].[usp_CreateTicket] TO [MaintenanceBoardApiRole];
    GRANT EXECUTE ON OBJECT::[tickets].[usp_TransitionTicketStatus] TO [MaintenanceBoardApiRole];
    GRANT EXECUTE ON OBJECT::[tickets].[usp_AssignTicket] TO [MaintenanceBoardApiRole];
    GRANT EXECUTE ON OBJECT::[tickets].[usp_AddTicketComment] TO [MaintenanceBoardApiRole];

    -- Same-owner stored procedures use ownership chaining; direct writes remain denied.
    DENY INSERT, UPDATE, DELETE ON SCHEMA::[access] TO [MaintenanceBoardApiRole];
    DENY INSERT, UPDATE, DELETE ON SCHEMA::[tickets] TO [MaintenanceBoardApiRole];
    DENY ALTER, TAKE OWNERSHIP ON SCHEMA::[access] TO [MaintenanceBoardApiRole];
    DENY ALTER, TAKE OWNERSHIP ON SCHEMA::[tickets] TO [MaintenanceBoardApiRole];

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
