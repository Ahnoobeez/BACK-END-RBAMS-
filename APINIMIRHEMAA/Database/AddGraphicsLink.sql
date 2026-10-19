-- Run once in each configured application database before deploying the API.
IF OBJECT_ID(N'dbo.Graphics', N'U') IS NULL
    THROW 50009, 'Required table dbo.Graphics was not found in the current database.', 1;

IF COL_LENGTH(N'dbo.Graphics', N'Link') IS NULL
    ALTER TABLE dbo.Graphics ADD [Link] nvarchar(2048) NULL;
