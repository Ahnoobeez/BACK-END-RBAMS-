-- Run against each configured application database before deploying the API.
IF OBJECT_ID(N'dbo.ServiceInvoiceFiles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ServiceInvoiceFiles
    (
        ServiceInvoiceFile_ID int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_ServiceInvoiceFiles PRIMARY KEY,
        FileName nvarchar(255) NOT NULL,
        FileExtension nvarchar(16) NOT NULL,
        FileData varbinary(max) NOT NULL,
        JobOrderID int NOT NULL,
        Status nvarchar(50) NOT NULL
            CONSTRAINT DF_ServiceInvoiceFiles_Status DEFAULT (N'Pending'),
        CreatedBy nvarchar(max) NULL,
        CONSTRAINT FK_ServiceInvoiceFiles_JobOrder
            FOREIGN KEY (JobOrderID) REFERENCES dbo.JobOrder (JobOrder_ID)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_ServiceInvoiceFiles_JobOrderID
        ON dbo.ServiceInvoiceFiles (JobOrderID);
END
GO
