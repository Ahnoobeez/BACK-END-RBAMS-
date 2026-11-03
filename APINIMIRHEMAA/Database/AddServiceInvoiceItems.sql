-- Run against each configured application database before deploying the API.
IF OBJECT_ID(N'dbo.ServiceInvoice', N'U') IS NULL
    THROW 50007, 'Required table dbo.ServiceInvoice was not found.', 1;
IF OBJECT_ID(N'dbo.JobOrder', N'U') IS NULL
    THROW 50008, 'Required table dbo.JobOrder was not found.', 1;
IF OBJECT_ID(N'dbo.JobOrderItems', N'U') IS NULL
    THROW 50010, 'Required table dbo.JobOrderItems was not found.', 1;
IF OBJECT_ID(N'dbo.Quotation_Items', N'U') IS NULL
    THROW 50011, 'Required table dbo.Quotation_Items was not found.', 1;

-- Quantity is an existing IDENTITY column, so the new key uses a sequence instead.
IF COL_LENGTH(N'dbo.ServiceInvoice', N'ServiceInvoice_ID') IS NULL
    ALTER TABLE dbo.ServiceInvoice ADD ServiceInvoice_ID int NULL;
IF COL_LENGTH(N'dbo.JobOrderItems', N'QI_ID') IS NULL
    ALTER TABLE dbo.JobOrderItems ADD QI_ID int NULL;
GO

DECLARE @NextServiceInvoiceId int;
SELECT @NextServiceInvoiceId = ISNULL(MAX(ServiceInvoice_ID), 0) + 1
FROM dbo.ServiceInvoice;

;WITH MissingIds AS
(
    SELECT
        ServiceInvoice_ID,
        ROW_NUMBER() OVER (ORDER BY Quantity) AS RowNumber
    FROM dbo.ServiceInvoice
    WHERE ServiceInvoice_ID IS NULL
)
UPDATE MissingIds
SET ServiceInvoice_ID = @NextServiceInvoiceId + CONVERT(int, RowNumber) - 1;

IF EXISTS (
    SELECT ServiceInvoice_ID
    FROM dbo.ServiceInvoice
    GROUP BY ServiceInvoice_ID
    HAVING COUNT(*) > 1
)
    THROW 50009, 'Duplicate ServiceInvoice_ID values were found.', 1;

ALTER TABLE dbo.ServiceInvoice ALTER COLUMN ServiceInvoice_ID int NOT NULL;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.JobOrderItems')
        AND name = N'FK_JobOrderItems_Quotation_Items'
)
BEGIN
    ALTER TABLE dbo.JobOrderItems
        ADD CONSTRAINT FK_JobOrderItems_Quotation_Items
        FOREIGN KEY (QI_ID) REFERENCES dbo.Quotation_Items (QI_ID)
        ON DELETE SET NULL;
END

DECLARE @PrimaryKey sysname;
SELECT @PrimaryKey = name
FROM sys.key_constraints
WHERE parent_object_id = OBJECT_ID(N'dbo.ServiceInvoice')
    AND type = N'PK';

IF NOT EXISTS (
    SELECT 1
    FROM sys.key_constraints AS keyConstraint
    JOIN sys.index_columns AS indexColumn
        ON indexColumn.object_id = keyConstraint.parent_object_id
        AND indexColumn.index_id = keyConstraint.unique_index_id
    JOIN sys.columns AS columnInfo
        ON columnInfo.object_id = indexColumn.object_id
        AND columnInfo.column_id = indexColumn.column_id
    WHERE keyConstraint.parent_object_id = OBJECT_ID(N'dbo.ServiceInvoice')
        AND keyConstraint.type = N'PK'
        AND columnInfo.name = N'ServiceInvoice_ID'
        AND indexColumn.key_ordinal = 1
        AND indexColumn.key_ordinal = (
            SELECT COUNT(*)
            FROM sys.index_columns AS allKeyColumns
            WHERE allKeyColumns.object_id = indexColumn.object_id
                AND allKeyColumns.index_id = indexColumn.index_id
                AND allKeyColumns.key_ordinal > 0
        )
)
BEGIN
    IF @PrimaryKey IS NOT NULL
    BEGIN
        DECLARE @DropPrimaryKeySql nvarchar(300);
        SET @DropPrimaryKeySql = N'ALTER TABLE dbo.ServiceInvoice DROP CONSTRAINT ' + QUOTENAME(@PrimaryKey);
        EXEC sys.sp_executesql @DropPrimaryKeySql;
    END

    ALTER TABLE dbo.ServiceInvoice
        ADD CONSTRAINT PK_ServiceInvoice PRIMARY KEY (ServiceInvoice_ID);
END

IF OBJECT_ID(N'dbo.ServiceInvoiceIdSequence', N'SO') IS NULL
BEGIN
    DECLARE @SequenceStart int;
    SELECT @SequenceStart = ISNULL(MAX(ServiceInvoice_ID), 0) + 1
    FROM dbo.ServiceInvoice;

    DECLARE @CreateSequenceSql nvarchar(300);
    SET @CreateSequenceSql =
        N'CREATE SEQUENCE dbo.ServiceInvoiceIdSequence AS int START WITH '
        + CONVERT(nvarchar(20), @SequenceStart)
        + N' INCREMENT BY 1;';
    EXEC sys.sp_executesql @CreateSequenceSql;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints AS defaultConstraint
    JOIN sys.columns AS columnInfo
        ON columnInfo.object_id = defaultConstraint.parent_object_id
        AND columnInfo.column_id = defaultConstraint.parent_column_id
    WHERE defaultConstraint.parent_object_id = OBJECT_ID(N'dbo.ServiceInvoice')
        AND columnInfo.name = N'ServiceInvoice_ID'
)
BEGIN
    ALTER TABLE dbo.ServiceInvoice
        ADD CONSTRAINT DF_ServiceInvoice_ServiceInvoice_ID
        DEFAULT (NEXT VALUE FOR dbo.ServiceInvoiceIdSequence) FOR ServiceInvoice_ID;
END

IF COL_LENGTH(N'dbo.ServiceInvoice', N'Invoice_Date') IS NULL
    ALTER TABLE dbo.ServiceInvoice
        ADD Invoice_Date date NOT NULL
            CONSTRAINT DF_ServiceInvoice_Invoice_Date DEFAULT (CONVERT(date, GETDATE()));

IF COL_LENGTH(N'dbo.ServiceInvoice', N'JobOrder_ID') IS NULL
    ALTER TABLE dbo.ServiceInvoice ADD JobOrder_ID int NULL;

ALTER TABLE dbo.ServiceInvoice ALTER COLUMN Unit nvarchar(50) NULL;
ALTER TABLE dbo.ServiceInvoice ALTER COLUMN Unit_Price decimal(18,2) NULL;
ALTER TABLE dbo.ServiceInvoice ALTER COLUMN Amount decimal(18,2) NULL;

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
GO

IF OBJECT_ID(N'dbo.ServiceInvoiceItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ServiceInvoiceItems
    (
        ServiceInvoiceItem_ID int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_ServiceInvoiceItems PRIMARY KEY,
        ServiceInvoice_ID int NOT NULL,
        Quantity int NOT NULL,
        Unit nvarchar(50) NOT NULL,
        Description nvarchar(max) NOT NULL,
        Unit_Price decimal(18,2) NOT NULL,
        Amount decimal(18,2) NOT NULL,
        CONSTRAINT FK_ServiceInvoiceItems_ServiceInvoice
            FOREIGN KEY (ServiceInvoice_ID) REFERENCES dbo.ServiceInvoice (ServiceInvoice_ID)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_ServiceInvoiceItems_ServiceInvoice_ID
        ON dbo.ServiceInvoiceItems (ServiceInvoice_ID);
END
GO

-- Preserve existing detail rows as single-line invoices.
INSERT INTO dbo.ServiceInvoiceItems
    (ServiceInvoice_ID, Quantity, Unit, Description, Unit_Price, Amount)
SELECT
    invoice.ServiceInvoice_ID,
    invoice.Quantity,
    COALESCE(CONVERT(nvarchar(50), invoice.Unit), N''),
    COALESCE(invoice.Description, N''),
    COALESCE(invoice.Unit_Price, 0),
    COALESCE(invoice.Amount, 0)
FROM dbo.ServiceInvoice AS invoice
WHERE invoice.Quantity IS NOT NULL
    AND NOT EXISTS (
        SELECT 1
        FROM dbo.ServiceInvoiceItems AS item
        WHERE item.ServiceInvoice_ID = invoice.ServiceInvoice_ID
    );
GO
