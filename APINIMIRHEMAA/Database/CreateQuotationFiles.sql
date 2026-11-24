IF OBJECT_ID(N'dbo.Quotation', N'U') IS NULL
    THROW 50010, 'Required table dbo.Quotation was not found in the current database.', 1;
GO

IF OBJECT_ID(N'dbo.QuotationFiles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.QuotationFiles
    (
        QuotationFile_ID int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_QuotationFiles PRIMARY KEY,
        FileName nvarchar(255) NOT NULL,
        FileExtension nvarchar(16) NOT NULL,
        FileData varbinary(max) NOT NULL,
        Quotation_ID int NOT NULL,
        CreatedBy nvarchar(max) NULL,
        CreatedByUserId int NULL,
        CreatedByDepartmentId int NULL,
        CONSTRAINT FK_QuotationFiles_Quotation
            FOREIGN KEY (Quotation_ID) REFERENCES dbo.Quotation (Quotation_ID)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_QuotationFiles_Quotation_ID
        ON dbo.QuotationFiles (Quotation_ID);
END;
GO
