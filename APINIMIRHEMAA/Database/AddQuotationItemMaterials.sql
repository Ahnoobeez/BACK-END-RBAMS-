IF OBJECT_ID(N'dbo.Quotation_Items', N'U') IS NULL
    THROW 50003, 'Required table dbo.Quotation_Items was not found in the current database.', 1;

IF OBJECT_ID(N'dbo.Materials', N'U') IS NULL
    THROW 50004, 'Required table dbo.Materials was not found in the current database.', 1;

IF OBJECT_ID(N'dbo.QuotationItemMaterial', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.QuotationItemMaterial
    (
        QuotationItemMaterial_ID int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_QuotationItemMaterial PRIMARY KEY,
        QI_ID int NOT NULL,
        Material_ID int NOT NULL,
        Code_Name nvarchar(max) NOT NULL,
        Item_Name nvarchar(max) NOT NULL,
        Unit nvarchar(max) NOT NULL,
        CONSTRAINT UQ_QuotationItemMaterial_Item_Material UNIQUE (QI_ID, Material_ID),
        CONSTRAINT FK_QuotationItemMaterial_Quotation_Items
            FOREIGN KEY (QI_ID) REFERENCES dbo.Quotation_Items(QI_ID) ON DELETE CASCADE,
        CONSTRAINT FK_QuotationItemMaterial_Materials
            FOREIGN KEY (Material_ID) REFERENCES dbo.Materials(Material_ID)
    );
END;
ELSE
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.QuotationItemMaterial')
          AND name = N'CK_QuotationItemMaterial_Quantity'
    )
        ALTER TABLE dbo.QuotationItemMaterial
            DROP CONSTRAINT CK_QuotationItemMaterial_Quantity;

    IF COL_LENGTH(N'dbo.QuotationItemMaterial', N'Quantity') IS NOT NULL
        ALTER TABLE dbo.QuotationItemMaterial
            DROP COLUMN Quantity;
END;
