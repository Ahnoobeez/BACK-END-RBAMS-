-- Run against the database used by /Marketing/AddLocalQuotationItems before deploying.
IF OBJECT_ID(N'dbo.Quotation_Items', N'U') IS NULL
    THROW 50002, 'Required table dbo.Quotation_Items was not found in the current database.', 1;

IF COL_LENGTH(N'dbo.Quotation_Items', N'Measurement_Unit') IS NULL
    ALTER TABLE dbo.Quotation_Items ADD Measurement_Unit nvarchar(8) NULL;

IF COL_LENGTH(N'dbo.Quotation_Items', N'Price_Per_Square_Inch') IS NULL
    ALTER TABLE dbo.Quotation_Items ADD Price_Per_Square_Inch decimal(18,4) NULL;

EXEC sys.sp_executesql N'
    ALTER TABLE dbo.Quotation_Items ALTER COLUMN Width decimal(18,4) NULL;
    ALTER TABLE dbo.Quotation_Items ALTER COLUMN Height decimal(18,4) NULL;
    ALTER TABLE dbo.Quotation_Items ALTER COLUMN Quantity decimal(18,4) NULL;
    ALTER TABLE dbo.Quotation_Items ALTER COLUMN Unit_Price decimal(18,2) NULL;
    ALTER TABLE dbo.Quotation_Items ALTER COLUMN Total_Amount decimal(18,2) NULL;

    UPDATE dbo.Quotation_Items
    SET Measurement_Unit = N''cm''
    WHERE Measurement_Unit IS NULL
        AND Width IS NOT NULL
        AND Height IS NOT NULL;';
