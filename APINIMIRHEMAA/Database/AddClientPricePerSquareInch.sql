-- Run against each database that contains dbo.Clients before deploying the updated API.
IF OBJECT_ID(N'dbo.Clients', N'U') IS NULL
    THROW 50003, 'Required table dbo.Clients was not found in the current database.', 1;

IF COL_LENGTH(N'dbo.Clients', N'Price_Per_Square_Inch') IS NULL
BEGIN
    ALTER TABLE dbo.Clients
    ADD Price_Per_Square_Inch decimal(18,4) NOT NULL
        CONSTRAINT DF_Clients_Price_Per_Square_Inch DEFAULT (0);
END
