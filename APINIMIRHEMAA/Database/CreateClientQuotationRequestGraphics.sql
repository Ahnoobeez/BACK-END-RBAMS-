IF OBJECT_ID(N'dbo.ClientQuotationRequestGraphics', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClientQuotationRequestGraphics
    (
        RequestGraphic_ID INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_ClientQuotationRequestGraphics PRIMARY KEY,
        Request_ID INT NOT NULL,
        FileName NVARCHAR(255) NOT NULL,
        FileExtension NVARCHAR(16) NOT NULL,
        ContentType NVARCHAR(100) NOT NULL,
        FileData VARBINARY(MAX) NOT NULL,
        CONSTRAINT FK_ClientQuotationRequestGraphics_ClientQuotationRequests
            FOREIGN KEY (Request_ID)
            REFERENCES dbo.ClientQuotationRequests (Request_ID)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_ClientQuotationRequestGraphics_Request_ID
        ON dbo.ClientQuotationRequestGraphics (Request_ID);
END;
