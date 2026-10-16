-- Run in both configured databases after RemoveClientsProject.sql; safe to rerun.
IF OBJECT_ID(N'dbo.Quotation', N'U') IS NULL
    THROW 50021, 'Required table dbo.Quotation was not found in the current database.', 1;

IF COL_LENGTH(N'dbo.Quotation', N'Client_ID') IS NULL
    THROW 50022, 'Run RemoveClientsProject.sql before this script to migrate quotations to clients.', 1;

IF COL_LENGTH(N'dbo.Quotation', N'Client_Subject') IS NULL
    ALTER TABLE dbo.Quotation ADD Client_Subject nvarchar(max) NULL;

IF OBJECT_ID(N'dbo.ClientQuotationSequence', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClientQuotationSequence
    (
        Client_ID int NOT NULL,
        SequenceYear int NOT NULL,
        SequenceMonth tinyint NOT NULL,
        LastSequence int NOT NULL,
        CONSTRAINT PK_ClientQuotationSequence
            PRIMARY KEY (Client_ID, SequenceYear, SequenceMonth),
        CONSTRAINT FK_ClientQuotationSequence_Clients
            FOREIGN KEY (Client_ID) REFERENCES dbo.Clients (Client_ID),
        CONSTRAINT CK_ClientQuotationSequence_Month
            CHECK (SequenceMonth BETWEEN 1 AND 12),
        CONSTRAINT CK_ClientQuotationSequence_LastSequence
            CHECK (LastSequence > 0)
    );
END

IF OBJECT_ID(N'dbo.ClientQuotationReservation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClientQuotationReservation
    (
        Reservation_ID uniqueidentifier NOT NULL
            CONSTRAINT PK_ClientQuotationReservation PRIMARY KEY,
        Client_ID int NOT NULL,
        SequenceYear int NOT NULL,
        SequenceMonth tinyint NOT NULL,
        SequenceNumber int NOT NULL,
        Client_Subject nvarchar(512) NOT NULL,
        ReservedBy nvarchar(200) NOT NULL,
        ReservedAt datetime2 NOT NULL,
        IsUsed bit NOT NULL,
        CONSTRAINT FK_ClientQuotationReservation_Clients
            FOREIGN KEY (Client_ID) REFERENCES dbo.Clients (Client_ID),
        CONSTRAINT CK_ClientQuotationReservation_Month
            CHECK (SequenceMonth BETWEEN 1 AND 12),
        CONSTRAINT CK_ClientQuotationReservation_SequenceNumber
            CHECK (SequenceNumber > 0)
    );
END
ELSE
BEGIN
    ALTER TABLE dbo.ClientQuotationReservation
        ALTER COLUMN Client_Subject nvarchar(512) NOT NULL;
END

;WITH ExistingQuotationCounts AS
(
    SELECT
        quotation.Client_ID,
        YEAR(quotation.Start_Date) AS SequenceYear,
        MONTH(quotation.Start_Date) AS SequenceMonth,
        COUNT_BIG(*) AS LastSequence
    FROM dbo.Quotation AS quotation
    GROUP BY quotation.Client_ID, YEAR(quotation.Start_Date), MONTH(quotation.Start_Date)
)
INSERT INTO dbo.ClientQuotationSequence (Client_ID, SequenceYear, SequenceMonth, LastSequence)
SELECT
    counts.Client_ID,
    counts.SequenceYear,
    counts.SequenceMonth,
    CONVERT(int, counts.LastSequence)
FROM ExistingQuotationCounts AS counts
WHERE counts.LastSequence <= 2147483647
    AND NOT EXISTS
    (
        SELECT 1
        FROM dbo.ClientQuotationSequence AS sequence
        WHERE sequence.Client_ID = counts.Client_ID
            AND sequence.SequenceYear = counts.SequenceYear
            AND sequence.SequenceMonth = counts.SequenceMonth
    );
