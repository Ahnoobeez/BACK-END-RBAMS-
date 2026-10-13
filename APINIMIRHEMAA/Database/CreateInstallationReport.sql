IF OBJECT_ID(N'dbo.Installation_Report', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Installation_Report
    (
        InstallationReport_ID int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Installation_Report PRIMARY KEY,
        JobOrder_ID int NOT NULL,
        ClientName nvarchar(200) NULL,
        ProjectName nvarchar(200) NULL,
        Location nvarchar(500) NULL,
        ReportDate date NULL,
        TimeStarted time(0) NULL,
        TimeEnded time(0) NULL,
        Installers nvarchar(1000) NULL,
        OnTime nvarchar(3) NULL,
        UniformAndId nvarchar(3) NULL,
        GraphicsProperlyInstalled nvarchar(3) NULL,
        ProblemsOccurred nvarchar(3) NULL,
        ProblemsNote nvarchar(2000) NULL,
        AreaClean nvarchar(3) NULL,
        InstallersMannered nvarchar(3) NULL,
        ClientRemarks nvarchar(2000) NULL,
        Obstruction nvarchar(3) NULL,
        ObstructionDetails nvarchar(2000) NULL,
        GraphicsDamaged nvarchar(3) NULL,
        DamageDetails nvarchar(2000) NULL,
        InstallerRemarks nvarchar(2000) NULL,
        ClientSignature nvarchar(200) NULL,
        PreparedBy nvarchar(200) NULL,
        ApprovedBy nvarchar(200) NULL,
        CreatedAtUtc datetime2(0) NOT NULL
            CONSTRAINT DF_Installation_Report_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Installation_Report_JobOrder
            FOREIGN KEY (JobOrder_ID) REFERENCES dbo.JobOrder (JobOrder_ID),
        CONSTRAINT CK_Installation_Report_OnTime
            CHECK (OnTime IS NULL OR OnTime IN (N'Yes', N'No')),
        CONSTRAINT CK_Installation_Report_UniformAndId
            CHECK (UniformAndId IS NULL OR UniformAndId IN (N'Yes', N'No')),
        CONSTRAINT CK_Installation_Report_GraphicsProperlyInstalled
            CHECK (GraphicsProperlyInstalled IS NULL OR GraphicsProperlyInstalled IN (N'Yes', N'No')),
        CONSTRAINT CK_Installation_Report_ProblemsOccurred
            CHECK (ProblemsOccurred IS NULL OR ProblemsOccurred IN (N'Yes', N'No')),
        CONSTRAINT CK_Installation_Report_AreaClean
            CHECK (AreaClean IS NULL OR AreaClean IN (N'Yes', N'No')),
        CONSTRAINT CK_Installation_Report_InstallersMannered
            CHECK (InstallersMannered IS NULL OR InstallersMannered IN (N'Yes', N'No')),
        CONSTRAINT CK_Installation_Report_Obstruction
            CHECK (Obstruction IS NULL OR Obstruction IN (N'Yes', N'No')),
        CONSTRAINT CK_Installation_Report_GraphicsDamaged
            CHECK (GraphicsDamaged IS NULL OR GraphicsDamaged IN (N'Yes', N'No'))
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Installation_Report_JobOrder_ID'
      AND object_id = OBJECT_ID(N'dbo.Installation_Report')
)
BEGIN
    CREATE INDEX IX_Installation_Report_JobOrder_ID
        ON dbo.Installation_Report (JobOrder_ID);
END;
GO
