-- Run with client writes paused against each database that contains dbo.Clients.
IF OBJECT_ID(N'dbo.Clients', N'U') IS NULL
    THROW 50004, 'Required table dbo.Clients was not found in the current database.', 1;

IF COL_LENGTH(N'dbo.Clients', N'Client_ID') IS NULL
    THROW 50005, 'Required column dbo.Clients.Client_ID was not found.', 1;

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Clients')
        AND name = N'Client_ID'
        AND is_identity = 1
)
    RETURN;

IF EXISTS (
    SELECT 1
    FROM sys.default_constraints AS defaults
    INNER JOIN sys.columns AS columns
        ON columns.object_id = defaults.parent_object_id
        AND columns.column_id = defaults.parent_column_id
    WHERE defaults.parent_object_id = OBJECT_ID(N'dbo.Clients')
        AND columns.name = N'Client_ID'
)
    RETURN;

DECLARE @nextClientId bigint;
SELECT @nextClientId = COALESCE(MAX(CONVERT(bigint, Client_ID)), 0) + 1
FROM dbo.Clients;

IF @nextClientId > 2147483647
    THROW 50006, 'dbo.Clients.Client_ID has exhausted the SQL int range.', 1;

IF OBJECT_ID(N'dbo.ClientIdDefaultSequence', N'SO') IS NULL
BEGIN
    DECLARE @createSequence nvarchar(max) =
        N'CREATE SEQUENCE dbo.ClientIdDefaultSequence AS int START WITH '
        + CONVERT(nvarchar(20), @nextClientId)
        + N' INCREMENT BY 1;';
    EXEC sys.sp_executesql @createSequence;
END;

ALTER TABLE dbo.Clients
    ADD CONSTRAINT DF_Clients_Client_ID_Generated
        DEFAULT (NEXT VALUE FOR dbo.ClientIdDefaultSequence) FOR Client_ID;
