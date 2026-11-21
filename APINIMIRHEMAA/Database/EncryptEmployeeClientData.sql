






SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS
(
    SELECT 1
    FROM sys.indexes AS indexes
    INNER JOIN sys.index_columns AS indexColumns
        ON indexes.object_id = indexColumns.object_id
        AND indexes.index_id = indexColumns.index_id
    INNER JOIN sys.columns AS columns
        ON columns.object_id = indexColumns.object_id
        AND columns.column_id = indexColumns.column_id
    WHERE indexes.object_id IN
    (
        OBJECT_ID(N'dbo.Clients'),
        OBJECT_ID(N'dbo.Employee')
    )
)
AND EXISTS
(
    SELECT 1
    FROM sys.indexes AS indexes
    INNER JOIN sys.index_columns AS indexColumns
        ON indexes.object_id = indexColumns.object_id
        AND indexes.index_id = indexColumns.index_id
    INNER JOIN sys.columns AS columns
        ON columns.object_id = indexColumns.object_id
        AND columns.column_id = indexColumns.column_id
    WHERE columns.name IN
    (
        N'CreatedBy', N'Client_Name', N'Client_Telephone', N'Contact_Person',
        N'Business_Style', N'Client_EmailAddress', N'TermsAndCondition',
        N'Company_Address', N'TIN', N'Payment_Terms', N'Price_Per_Square_Inch',
        N'LastName', N'FirstName', N'MiddleName', N'status', N'Birthday',
        N'ContactNumber', N'SSS_Number', N'Philhealth_Number', N'Pagibig_Number',
        N'Date_Started', N'Date_Resigned'
    )
    AND indexes.object_id IN
    (
        OBJECT_ID(N'dbo.Clients'),
        OBJECT_ID(N'dbo.Employee')
    )
)
BEGIN
    THROW 51000, 'Remove or redesign indexes that include Employee/Client private fields before widening those columns.', 1;
END;

DECLARE @DropPrivateFieldDefaultsSql nvarchar(max) = N'';
SELECT @DropPrivateFieldDefaultsSql = @DropPrivateFieldDefaultsSql
    + N'ALTER TABLE ' + QUOTENAME(schemas.name) + N'.' + QUOTENAME(tables.name)
    + N' DROP CONSTRAINT ' + QUOTENAME(defaults.name) + N';'
FROM sys.default_constraints AS defaults
INNER JOIN sys.tables AS tables
    ON tables.object_id = defaults.parent_object_id
INNER JOIN sys.schemas AS schemas
    ON schemas.schema_id = tables.schema_id
INNER JOIN sys.columns AS columns
    ON columns.object_id = tables.object_id
    AND columns.column_id = defaults.parent_column_id
WHERE schemas.name = N'dbo'
    AND tables.name IN (N'Clients', N'Employee')
    AND columns.name IN
    (
        N'CreatedBy', N'Client_Name', N'Client_Telephone', N'Contact_Person',
        N'Business_Style', N'Client_EmailAddress', N'TermsAndCondition',
        N'Company_Address', N'TIN', N'Payment_Terms', N'Price_Per_Square_Inch',
        N'LastName', N'FirstName', N'MiddleName', N'status', N'Birthday',
        N'ContactNumber', N'SSS_Number', N'Philhealth_Number', N'Pagibig_Number',
        N'Date_Started', N'Date_Resigned'
    );

IF @DropPrivateFieldDefaultsSql <> N''
BEGIN
    EXEC sys.sp_executesql @DropPrivateFieldDefaultsSql;
END;

IF OBJECT_ID(N'dbo.Clients', N'U') IS NOT NULL
BEGIN
    EXEC(N'
        ALTER TABLE dbo.Clients ALTER COLUMN CreatedBy nvarchar(max) NULL;
        ALTER TABLE dbo.Clients ALTER COLUMN Client_Name nvarchar(max) NOT NULL;
        ALTER TABLE dbo.Clients ALTER COLUMN Client_Telephone nvarchar(max) NOT NULL;
        ALTER TABLE dbo.Clients ALTER COLUMN Contact_Person nvarchar(max) NULL;
        ALTER TABLE dbo.Clients ALTER COLUMN Business_Style nvarchar(max) NOT NULL;
        ALTER TABLE dbo.Clients ALTER COLUMN Client_EmailAddress nvarchar(max) NULL;
        ALTER TABLE dbo.Clients ALTER COLUMN TermsAndCondition nvarchar(max) NOT NULL;
        ALTER TABLE dbo.Clients ALTER COLUMN Company_Address nvarchar(max) NULL;
        ALTER TABLE dbo.Clients ALTER COLUMN TIN nvarchar(max) NOT NULL;
        ALTER TABLE dbo.Clients ALTER COLUMN Payment_Terms nvarchar(max) NULL;
        ALTER TABLE dbo.Clients ALTER COLUMN Price_Per_Square_Inch nvarchar(max) NOT NULL;
    ');
END;

IF OBJECT_ID(N'dbo.Employee', N'U') IS NOT NULL
BEGIN
    EXEC(N'
        ALTER TABLE dbo.Employee ALTER COLUMN LastName nvarchar(max) NULL;
        ALTER TABLE dbo.Employee ALTER COLUMN FirstName nvarchar(max) NULL;
        ALTER TABLE dbo.Employee ALTER COLUMN MiddleName nvarchar(max) NULL;
        ALTER TABLE dbo.Employee ALTER COLUMN status nvarchar(max) NULL;
        ALTER TABLE dbo.Employee ALTER COLUMN Birthday nvarchar(max) NULL;
        ALTER TABLE dbo.Employee ALTER COLUMN ContactNumber nvarchar(max) NULL;
        ALTER TABLE dbo.Employee ALTER COLUMN SSS_Number nvarchar(max) NULL;
        ALTER TABLE dbo.Employee ALTER COLUMN Philhealth_Number nvarchar(max) NULL;
        ALTER TABLE dbo.Employee ALTER COLUMN Pagibig_Number nvarchar(max) NULL;
        ALTER TABLE dbo.Employee ALTER COLUMN Date_Started nvarchar(max) NULL;
        ALTER TABLE dbo.Employee ALTER COLUMN Date_Resigned nvarchar(max) NULL;
    ');
END;

COMMIT TRANSACTION;
