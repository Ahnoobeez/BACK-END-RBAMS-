IF COL_LENGTH(N'dbo.Employee', N'IsArchived') IS NULL
BEGIN
    ALTER TABLE dbo.Employee
        ADD IsArchived bit NOT NULL
            CONSTRAINT DF_Employee_IsArchived DEFAULT (0) WITH VALUES;
END;
