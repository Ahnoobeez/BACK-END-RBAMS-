-- Run in each application database before deploying the API.
IF OBJECT_ID(N'dbo.JobOrder', N'U') IS NULL
    THROW 50005, 'Required table dbo.JobOrder was not found in the current database.', 1;

IF OBJECT_ID(N'dbo.JobOrderComment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.JobOrderComment
    (
        JobOrderComment_ID int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_JobOrderComment PRIMARY KEY,
        JobOrder_ID int NOT NULL,
        Employee_Name nvarchar(200) NOT NULL,
        [Comment] nvarchar(max) NOT NULL,
        CreatedAt datetime2(7) NOT NULL
            CONSTRAINT DF_JobOrderComment_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_JobOrderComment_JobOrder
            FOREIGN KEY (JobOrder_ID) REFERENCES dbo.JobOrder(JobOrder_ID) ON DELETE CASCADE
    );

END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.JobOrderComment')
      AND name = N'IX_JobOrderComment_JobOrder_ID_JobOrderComment_ID'
)
    CREATE INDEX IX_JobOrderComment_JobOrder_ID_JobOrderComment_ID
        ON dbo.JobOrderComment(JobOrder_ID, JobOrderComment_ID);

IF COL_LENGTH(N'dbo.JobOrder', N'Comment') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.JobOrder', N'CreatedBy') IS NOT NULL
    BEGIN
        INSERT INTO dbo.JobOrderComment (JobOrder_ID, Employee_Name, [Comment])
        SELECT
            jobOrder.JobOrder_ID,
            LEFT(COALESCE(NULLIF(LTRIM(RTRIM(jobOrder.CreatedBy)), N''), N'Previous comment'), 200),
            jobOrder.[Comment]
        FROM dbo.JobOrder AS jobOrder
        WHERE NULLIF(LTRIM(RTRIM(jobOrder.[Comment])), N'') IS NOT NULL;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.JobOrderComment (JobOrder_ID, Employee_Name, [Comment])
        SELECT jobOrder.JobOrder_ID, N'Previous comment', jobOrder.[Comment]
        FROM dbo.JobOrder AS jobOrder
        WHERE NULLIF(LTRIM(RTRIM(jobOrder.[Comment])), N'') IS NOT NULL;
    END;

    ALTER TABLE dbo.JobOrder DROP COLUMN [Comment];
END;
