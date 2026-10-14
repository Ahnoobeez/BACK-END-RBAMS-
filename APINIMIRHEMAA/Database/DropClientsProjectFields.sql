IF OBJECT_ID(N'dbo.ClientsProject', N'U') IS NULL
    THROW 50001, 'Required table dbo.ClientsProject was not found in the current database.', 1;

IF COL_LENGTH(N'dbo.ClientsProject', N'Attention') IS NOT NULL
    ALTER TABLE [dbo].[ClientsProject] DROP COLUMN [Attention];

IF COL_LENGTH(N'dbo.ClientsProject', N'Representative') IS NOT NULL
    ALTER TABLE [dbo].[ClientsProject] DROP COLUMN [Representative];

IF COL_LENGTH(N'dbo.ClientsProject', N'Account_Executive') IS NOT NULL
    ALTER TABLE [dbo].[ClientsProject] DROP COLUMN [Account_Executive];
