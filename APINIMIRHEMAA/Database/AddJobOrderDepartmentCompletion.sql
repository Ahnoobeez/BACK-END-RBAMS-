-- Run once in each configured application database before deploying the API.
IF OBJECT_ID(N'dbo.JobOrder', N'U') IS NULL
    THROW 50010, 'Required table dbo.JobOrder was not found in the current database.', 1;

IF COL_LENGTH(N'dbo.JobOrder', N'Production_Completed') IS NULL
    ALTER TABLE dbo.JobOrder
        ADD Production_Completed bit NOT NULL
            CONSTRAINT DF_JobOrder_Production_Completed DEFAULT (0) WITH VALUES;

IF COL_LENGTH(N'dbo.JobOrder', N'Technical_Completed') IS NULL
    ALTER TABLE dbo.JobOrder
        ADD Technical_Completed bit NOT NULL
            CONSTRAINT DF_JobOrder_Technical_Completed DEFAULT (0) WITH VALUES;
