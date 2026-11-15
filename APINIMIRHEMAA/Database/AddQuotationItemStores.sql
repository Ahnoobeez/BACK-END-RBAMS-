IF OBJECT_ID(N'dbo.Quotation_Items', N'U') IS NULL
    THROW 50005, 'Required table dbo.Quotation_Items was not found in the current database.', 1;

IF OBJECT_ID(N'dbo.Quotation_Store', N'U') IS NULL
    THROW 50006, 'Required table dbo.Quotation_Store was not found in the current database.', 1;

IF OBJECT_ID(N'dbo.QuotationItemStores', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.QuotationItemStores
    (
        QI_ID int NOT NULL,
        QS_ID int NOT NULL,
        CreatedBy nvarchar(200) NULL,
        CreatedByUserId int NULL,
        CreatedByDepartmentId int NULL,
        CONSTRAINT PK_QuotationItemStores PRIMARY KEY (QI_ID, QS_ID),
        CONSTRAINT FK_QuotationItemStores_Quotation_Items
            FOREIGN KEY (QI_ID) REFERENCES dbo.Quotation_Items(QI_ID) ON DELETE CASCADE,
        CONSTRAINT FK_QuotationItemStores_Quotation_Store
            FOREIGN KEY (QS_ID) REFERENCES dbo.Quotation_Store(QS_ID) ON DELETE CASCADE
    );

    CREATE INDEX IX_QuotationItemStores_QS_ID
        ON dbo.QuotationItemStores(QS_ID);
END;
