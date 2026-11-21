
IF OBJECT_ID(N'dbo.ServiceInvoice', N'U') IS NULL
    THROW 50032, 'Required table dbo.ServiceInvoice was not found.', 1;
IF OBJECT_ID(N'dbo.ServiceInvoiceItems', N'U') IS NULL
    THROW 50033, 'Required table dbo.ServiceInvoiceItems was not found.', 1;

IF COL_LENGTH(N'dbo.ServiceInvoice', N'Branch') IS NULL
    ALTER TABLE dbo.ServiceInvoice ADD Branch nvarchar(max) NULL;
IF COL_LENGTH(N'dbo.ServiceInvoice', N'Title') IS NULL
    ALTER TABLE dbo.ServiceInvoice ADD Title nvarchar(max) NULL;
IF COL_LENGTH(N'dbo.ServiceInvoice', N'ClientName') IS NULL
    ALTER TABLE dbo.ServiceInvoice ADD ClientName nvarchar(max) NULL;
IF COL_LENGTH(N'dbo.ServiceInvoice', N'Address') IS NULL
    ALTER TABLE dbo.ServiceInvoice ADD Address nvarchar(max) NULL;
IF COL_LENGTH(N'dbo.ServiceInvoice', N'Terms') IS NULL
    ALTER TABLE dbo.ServiceInvoice ADD Terms nvarchar(max) NULL;

IF COL_LENGTH(N'dbo.ServiceInvoiceItems', N'Store') IS NULL
    ALTER TABLE dbo.ServiceInvoiceItems ADD Store nvarchar(max) NULL;
IF COL_LENGTH(N'dbo.ServiceInvoiceItems', N'Requirement') IS NULL
    ALTER TABLE dbo.ServiceInvoiceItems ADD Requirement nvarchar(max) NULL;
