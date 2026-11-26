IF OBJECT_ID(N'dbo.ClientQuotationRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClientQuotationRequests
    (
        Request_ID int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Client_Name nvarchar(max) NOT NULL,
        Client_Telephone nvarchar(max) NOT NULL,
        Contact_Person nvarchar(max) NOT NULL,
        Business_Style nvarchar(max) NOT NULL,
        Client_EmailAddress nvarchar(max) NOT NULL,
        TermsAndCondition nvarchar(max) NOT NULL,
        Company_Address nvarchar(max) NOT NULL,
        TIN nvarchar(max) NOT NULL,
        Payment_Terms nvarchar(max) NOT NULL,
        Project_Title nvarchar(max) NOT NULL,
        Items_Json nvarchar(max) NOT NULL,
        Sub_Total decimal(18,2) NOT NULL,
        Delivery_Charge decimal(18,2) NOT NULL,
        Installation_Charge decimal(18,2) NOT NULL,
        VAT decimal(18,2) NOT NULL,
        Overall_Total decimal(18,2) NOT NULL,
        SubmittedAtUtc datetime2 NOT NULL CONSTRAINT DF_ClientQuotationRequests_SubmittedAtUtc DEFAULT SYSUTCDATETIME(),
        Status nvarchar(32) NOT NULL CONSTRAINT DF_ClientQuotationRequests_Status DEFAULT N'Pending',
        ClaimedByUserId int NULL,
        ClaimedAtUtc datetime2 NULL,
        Client_ID int NULL,
        Quotation_ID int NULL
    );

END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ClientQuotationRequests')
      AND name = N'IX_ClientQuotationRequests_Status_SubmittedAtUtc'
)
    CREATE INDEX IX_ClientQuotationRequests_Status_SubmittedAtUtc
        ON dbo.ClientQuotationRequests (Status, SubmittedAtUtc DESC);

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ClientQuotationRequests')
      AND name = N'IX_ClientQuotationRequests_ClaimedByUserId'
)
    CREATE INDEX IX_ClientQuotationRequests_ClaimedByUserId
        ON dbo.ClientQuotationRequests (ClaimedByUserId);
