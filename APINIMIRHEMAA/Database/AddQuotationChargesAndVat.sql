-- Run against each Local and Cloud database that contains dbo.Quotation.
-- Legacy discount and ingress/egress columns are left intact to preserve existing records.
IF OBJECT_ID(N'dbo.Quotation', N'U') IS NULL
    THROW 50003, 'Required table dbo.Quotation was not found in the current database.', 1;

IF COL_LENGTH(N'dbo.Quotation', N'Delivery_Charge') IS NULL
    ALTER TABLE dbo.Quotation ADD Delivery_Charge decimal(18,2) NULL;

IF COL_LENGTH(N'dbo.Quotation', N'Installation_Charge') IS NULL
    ALTER TABLE dbo.Quotation ADD Installation_Charge decimal(18,2) NULL;

IF COL_LENGTH(N'dbo.Quotation', N'VAT') IS NULL
    ALTER TABLE dbo.Quotation ADD VAT decimal(18,2) NULL;

EXEC sys.sp_executesql N'
    ALTER TABLE dbo.Quotation ALTER COLUMN Sub_Total decimal(18,2) NOT NULL;
    ALTER TABLE dbo.Quotation ALTER COLUMN Overall_Total decimal(18,2) NOT NULL;';
