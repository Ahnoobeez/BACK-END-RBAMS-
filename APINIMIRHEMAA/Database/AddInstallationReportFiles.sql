IF OBJECT_ID(N'dbo.InstallationReportFiles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InstallationReportFiles
    (
        InstallationReportFile_ID int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_InstallationReportFiles PRIMARY KEY,
        InstallationReport_ID int NOT NULL,
        File_Role nvarchar(20) NOT NULL,
        File_Name nvarchar(255) NOT NULL,
        File_Extension nvarchar(10) NOT NULL,
        File_Data varbinary(max) NOT NULL,
        CreatedBy nvarchar(200) NULL,
        CreatedByUserId int NULL,
        CreatedByDepartmentId int NULL,
        CONSTRAINT FK_InstallationReportFiles_Installation_Report
            FOREIGN KEY (InstallationReport_ID)
            REFERENCES dbo.Installation_Report (InstallationReport_ID)
            ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_InstallationReportFiles_InstallationReport_ID'
      AND object_id = OBJECT_ID(N'dbo.InstallationReportFiles')
)
BEGIN
    CREATE INDEX IX_InstallationReportFiles_InstallationReport_ID
        ON dbo.InstallationReportFiles (InstallationReport_ID);
END;
GO
