-- Run this once against both the LocalDatabase and AzureDatabase before deploying the API.
IF OBJECT_ID(N'dbo.Clients', N'U') IS NULL
    THROW 50000, 'dbo.Clients was not found in the current database.', 1;

IF COL_LENGTH(N'dbo.Clients', N'Client_Telephone') IS NULL
    THROW 50001, 'dbo.Clients.Client_Telephone was not found.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Clients')
      AND name = N'Client_Telephone'
      AND system_type_id = TYPE_ID(N'nvarchar')
      AND max_length = 32
)
BEGIN
    ALTER TABLE dbo.Clients
    ALTER COLUMN Client_Telephone nvarchar(16) NOT NULL;
END;

IF COL_LENGTH(N'dbo.Clients', N'Contact_Person') IS NULL
BEGIN
    ALTER TABLE dbo.Clients
    ADD Contact_Person nvarchar(200) NULL;
END;

IF COL_LENGTH(N'dbo.Clients', N'Client_EmailAddress') IS NULL
BEGIN
    ALTER TABLE dbo.Clients
    ADD Client_EmailAddress nvarchar(254) NULL;
END;
