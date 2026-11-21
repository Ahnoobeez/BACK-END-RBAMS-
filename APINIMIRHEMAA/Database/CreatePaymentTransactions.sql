
IF OBJECT_ID(N'dbo.JobOrder', N'U') IS NULL
    THROW 50020, 'Required table dbo.JobOrder was not found in the current database.', 1;

IF OBJECT_ID(N'dbo.Quotation', N'U') IS NULL
    THROW 50021, 'Required table dbo.Quotation was not found in the current database.', 1;

IF OBJECT_ID(N'dbo.CollectionReceipt', N'U') IS NULL
    THROW 50022, 'Required table dbo.CollectionReceipt was not found in the current database.', 1;

IF OBJECT_ID(N'dbo.PaymentTransaction', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PaymentTransaction
    (
        PaymentTransaction_ID int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_PaymentTransaction PRIMARY KEY,
        CollectionReceipt_ID int NULL,
        JobOrderID int NOT NULL,
        Quotation_ID int NOT NULL,
        Amount decimal(18,2) NOT NULL,
        PaidAtUtc datetime2(7) NOT NULL
            CONSTRAINT DF_PaymentTransaction_PaidAtUtc DEFAULT SYSUTCDATETIME(),
        CreatedBy nvarchar(max) NULL,
        CreatedByUserId int NULL,
        CreatedByDepartmentId int NULL,
        CONSTRAINT FK_PaymentTransaction_CollectionReceipt
            FOREIGN KEY (CollectionReceipt_ID)
            REFERENCES dbo.CollectionReceipt (CollectionReceipt_ID)
            ON DELETE SET NULL,
        CONSTRAINT FK_PaymentTransaction_JobOrder
            FOREIGN KEY (JobOrderID) REFERENCES dbo.JobOrder (JobOrder_ID),
        CONSTRAINT FK_PaymentTransaction_Quotation
            FOREIGN KEY (Quotation_ID) REFERENCES dbo.Quotation (Quotation_ID)
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PaymentTransaction')
      AND name = N'UX_PaymentTransaction_JobOrderID'
)
    CREATE UNIQUE INDEX UX_PaymentTransaction_JobOrderID
        ON dbo.PaymentTransaction (JobOrderID);

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PaymentTransaction')
      AND name = N'UX_PaymentTransaction_CollectionReceipt_ID'
)
    CREATE UNIQUE INDEX UX_PaymentTransaction_CollectionReceipt_ID
        ON dbo.PaymentTransaction (CollectionReceipt_ID)
        WHERE CollectionReceipt_ID IS NOT NULL;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PaymentTransaction')
      AND name = N'IX_PaymentTransaction_PaidAtUtc'
)
    CREATE INDEX IX_PaymentTransaction_PaidAtUtc
        ON dbo.PaymentTransaction (PaidAtUtc DESC);
GO
