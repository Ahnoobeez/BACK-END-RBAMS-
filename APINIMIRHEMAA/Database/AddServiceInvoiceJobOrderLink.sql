-- Run against each database containing dbo.ServiceInvoice before deploying the updated API.
IF OBJECT_ID(N'dbo.ServiceInvoice', N'U') IS NULL
    THROW 50007, 'Required table dbo.ServiceInvoice was not found in the current database.', 1;

IF OBJECT_ID(N'dbo.JobOrder', N'U') IS NULL
    THROW 50008, 'Required table dbo.JobOrder was not found in the current database.', 1;

IF COL_LENGTH(N'dbo.ServiceInvoice', N'JobOrder_ID') IS NULL
    ALTER TABLE dbo.ServiceInvoice ADD JobOrder_ID int NULL;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.ServiceInvoice')
        AND name = N'FK_ServiceInvoice_JobOrder'
)
BEGIN
    ALTER TABLE dbo.ServiceInvoice
        WITH CHECK ADD CONSTRAINT FK_ServiceInvoice_JobOrder
        FOREIGN KEY (JobOrder_ID) REFERENCES dbo.JobOrder (JobOrder_ID);
END
