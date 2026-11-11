-- Run once in both configured application databases before deploying the API.
-- Existing CreatedBy names are linked only when they identify exactly one account.
-- Unmatched or ambiguous legacy records remain available to Admin and Super Admin.
GO

DECLARE @Tables TABLE (TableName sysname PRIMARY KEY);
INSERT INTO @Tables (TableName)
VALUES
    (N'Clients'),
    (N'Quotation'),
    (N'Quotation_Items'),
    (N'Quotation_Store'),
    (N'QuotationItemMaterial'),
    (N'Conforme'),
    (N'PurchaseOrder'),
    (N'JobOrder'),
    (N'JobOrderItems'),
    (N'JobOrderComment'),
    (N'DeliveryReceipt'),
    (N'DeliveryReceiptItems'),
    (N'DeliveryFiles'),
    (N'CollectionReceipt'),
    (N'ServiceInvoice'),
    (N'ServiceInvoiceItems'),
    (N'ServiceInvoiceFiles'),
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
    BEGIN
        IF COL_LENGTH(@QualifiedName, N'CreatedBy') IS NULL
        BEGIN
            SET @Sql = N'ALTER TABLE ' + @QualifiedName + N' ADD [CreatedBy] nvarchar(max) NULL;';
            EXEC sys.sp_executesql @Sql;
        END

        IF COL_LENGTH(@QualifiedName, N'CreatedByUserId') IS NULL
        BEGIN
            SET @Sql = N'ALTER TABLE ' + @QualifiedName + N' ADD [CreatedByUserId] int NULL;';
            EXEC sys.sp_executesql @Sql;
        END

        IF COL_LENGTH(@QualifiedName, N'CreatedByDepartmentId') IS NULL
        BEGIN
            SET @Sql = N'ALTER TABLE ' + @QualifiedName + N' ADD [CreatedByDepartmentId] int NULL;';
            EXEC sys.sp_executesql @Sql;
        END
    END
END

IF OBJECT_ID(N'dbo.JobOrderComment', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'
        UPDATE dbo.JobOrderComment
        SET CreatedBy = Employee_Name
        WHERE CreatedBy IS NULL;';

DECLARE ownership_tables CURSOR LOCAL FAST_FORWARD FOR
    SELECT TableName
    FROM (VALUES
        (N'Clients'),
        (N'Quotation'),
        (N'Quotation_Items'),
        (N'Quotation_Store'),
        (N'QuotationItemMaterial'),
        (N'Conforme'),
        (N'PurchaseOrder'),
        (N'JobOrder'),
        (N'JobOrderItems'),
        (N'JobOrderComment'),
        (N'DeliveryReceipt'),
        (N'DeliveryReceiptItems'),
        (N'DeliveryFiles'),
        (N'CollectionReceipt'),
        (N'ServiceInvoice'),
        (N'ServiceInvoiceItems'),
        (N'ServiceInvoiceFiles'),
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
        (N'IssuedBy')
    ) AS tables(TableName);

OPEN ownership_tables;
FETCH NEXT FROM ownership_tables INTO @TableName;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @QualifiedName = QUOTENAME(N'dbo') + N'.' + QUOTENAME(@TableName);
    IF OBJECT_ID(@QualifiedName, N'U') IS NOT NULL
    BEGIN
        IF OBJECT_ID(N'dbo.Employee', N'U') IS NOT NULL
        BEGIN
            SET @Sql = N'
                UPDATE record
                SET CreatedByUserId = owner.User_ID,
                    CreatedByDepartmentId = owner.Department_ID
                FROM ' + @QualifiedName + N' AS record
                CROSS APPLY
                (
                    SELECT MIN(account.User_ID) AS User_ID,
                           MIN(account.Department_ID) AS Department_ID
                    FROM dbo.Users AS account
                    LEFT JOIN dbo.Employee AS employee
                        ON employee.Employee_Id = account.Employee_ID
                    WHERE NULLIF(LTRIM(RTRIM(record.CreatedBy)), N'''') IS NOT NULL
                      AND
                      (
                          UPPER(LTRIM(RTRIM(record.CreatedBy))) =
                              UPPER(LTRIM(RTRIM(CONCAT(employee.FirstName, N'' '', employee.LastName))))
                          OR UPPER(LTRIM(RTRIM(record.CreatedBy))) =
                              UPPER(LTRIM(RTRIM(account.Username)))
                      )
                    HAVING COUNT(*) = 1
                ) AS owner
                WHERE record.CreatedByUserId IS NULL
                  AND owner.User_ID IS NOT NULL;';
        END
        ELSE
        BEGIN
            SET @Sql = N'
                UPDATE record
                SET CreatedByUserId = owner.User_ID,
                    CreatedByDepartmentId = owner.Department_ID
                FROM ' + @QualifiedName + N' AS record
                CROSS APPLY
                (
                    SELECT MIN(account.User_ID) AS User_ID,
                           MIN(account.Department_ID) AS Department_ID
                    FROM dbo.Users AS account
                    WHERE NULLIF(LTRIM(RTRIM(record.CreatedBy)), N'''') IS NOT NULL
                      AND UPPER(LTRIM(RTRIM(record.CreatedBy))) =
                          UPPER(LTRIM(RTRIM(account.Username)))
                    HAVING COUNT(*) = 1
                ) AS owner
                WHERE record.CreatedByUserId IS NULL
                  AND owner.User_ID IS NOT NULL;';
        END
        EXEC sys.sp_executesql @Sql;
    END
    FETCH NEXT FROM ownership_tables INTO @TableName;
END
CLOSE ownership_tables;
DEALLOCATE ownership_tables;

IF OBJECT_ID(N'dbo.QuotationItemMaterial', N'U') IS NOT NULL
    AND OBJECT_ID(N'dbo.Quotation_Items', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'
        UPDATE material
        SET material.CreatedBy = COALESCE(material.CreatedBy, item.CreatedBy),
            material.CreatedByUserId = item.CreatedByUserId,
            material.CreatedByDepartmentId = item.CreatedByDepartmentId
        FROM dbo.QuotationItemMaterial AS material
        INNER JOIN dbo.Quotation_Items AS item ON item.QI_ID = material.QI_ID
        WHERE material.CreatedByUserId IS NULL
          AND item.CreatedByUserId IS NOT NULL;';

IF OBJECT_ID(N'dbo.ServiceInvoiceItems', N'U') IS NOT NULL
    AND OBJECT_ID(N'dbo.ServiceInvoice', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'
        UPDATE item
        SET item.CreatedBy = COALESCE(item.CreatedBy, invoice.CreatedBy),
            item.CreatedByUserId = invoice.CreatedByUserId,
            item.CreatedByDepartmentId = invoice.CreatedByDepartmentId
        FROM dbo.ServiceInvoiceItems AS item
        INNER JOIN dbo.ServiceInvoice AS invoice
            ON invoice.ServiceInvoice_ID = item.ServiceInvoice_ID
        WHERE item.CreatedByUserId IS NULL
          AND invoice.CreatedByUserId IS NOT NULL;';

GO
