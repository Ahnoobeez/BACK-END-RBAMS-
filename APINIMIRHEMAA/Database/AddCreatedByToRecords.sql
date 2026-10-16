-- Run once in both configured application databases before deploying the API.
-- Existing records remain NULL; newly created records receive the signed-in employee's name.
GO

DECLARE @Tables TABLE (TableName sysname PRIMARY KEY);
INSERT INTO @Tables (TableName)
VALUES
    (N'Clients'),
    (N'Quotation'),
    (N'Quotation_Items'),
    (N'Quotation_Store'),
    (N'Conforme'),
    (N'PurchaseOrder'),
    (N'JobOrder'),
    (N'JobOrderItems'),
    (N'DeliveryReceipt'),
    (N'DeliveryReceiptItems'),
    (N'DeliveryFiles'),
    (N'CollectionReceipt'),
    (N'ServiceInvoice'),
    (N'Installation_Schedule'),
    (N'Installation_Schedule_Employee'),
    (N'Installation_Report'),
    (N'Graphics'),
    (N'MaterialRequisition_Slip'),
    (N'MaterailsRequisition_Slip'),
    (N'Materials'),
    (N'Inventory'),
    (N'Purchased_Material'),
    (N'StockTransfer_Slip'),
    (N'Transmittal_Slip'),
    (N'IssuedBy');

DECLARE @TableName sysname;
DECLARE @QualifiedName nvarchar(300);
DECLARE @Sql nvarchar(max);

WHILE EXISTS (SELECT 1 FROM @Tables)
BEGIN
    SELECT TOP (1) @TableName = TableName FROM @Tables ORDER BY TableName;
    DELETE FROM @Tables WHERE TableName = @TableName;

    SET @QualifiedName = QUOTENAME(N'dbo') + N'.' + QUOTENAME(@TableName);
    IF OBJECT_ID(@QualifiedName, N'U') IS NOT NULL
        AND COL_LENGTH(@QualifiedName, N'CreatedBy') IS NULL
    BEGIN
        SET @Sql = N'ALTER TABLE ' + @QualifiedName + N' ADD [CreatedBy] nvarchar(max) NULL;';
        EXEC sys.sp_executesql @Sql;
    END
END

IF OBJECT_ID(N'dbo.Quotation', N'U') IS NOT NULL
    AND OBJECT_ID(N'dbo.AuditTrail', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.Quotation', N'CreatedBy') IS NOT NULL
BEGIN
    UPDATE quotation
    SET CreatedBy = createdBy.Name
    FROM dbo.Quotation AS quotation
    CROSS APPLY (
        SELECT TOP (1) audit.Name
        FROM dbo.AuditTrail AS audit
        WHERE audit.Action = N'Quotation# '
            + CONVERT(nvarchar(20), quotation.Quotation_ID)
            + N' has been created'
            AND NULLIF(LTRIM(RTRIM(audit.Name)), N'') IS NOT NULL
        ORDER BY audit.AuditTrail_ID
    ) AS createdBy
    WHERE quotation.CreatedBy IS NULL;
END

GO
