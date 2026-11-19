IF OBJECT_ID(N'dbo.MaterialRequisition_Slip', N'U') IS NULL
   AND OBJECT_ID(N'dbo.MaterailsRequisition_Slip', N'U') IS NULL
BEGIN
    RAISERROR('No material requisition table was found in this database.', 16, 1);
    RETURN;
END;
GO

IF OBJECT_ID(N'dbo.MaterialRequisition_Slip', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.MaterialRequisition_Slip', N'Purchased_ID') IS NULL
        EXEC(N'ALTER TABLE dbo.MaterialRequisition_Slip ADD Purchased_ID int NULL;');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_MaterialRequisition_Slip_Purchased_ID'
          AND object_id = OBJECT_ID(N'dbo.MaterialRequisition_Slip')
    )
        EXEC(N'CREATE INDEX IX_MaterialRequisition_Slip_Purchased_ID ON dbo.MaterialRequisition_Slip (Purchased_ID);');
END;
GO

IF OBJECT_ID(N'dbo.MaterailsRequisition_Slip', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.MaterailsRequisition_Slip', N'Purchased_ID') IS NULL
        EXEC(N'ALTER TABLE dbo.MaterailsRequisition_Slip ADD Purchased_ID int NULL;');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_MaterailsRequisition_Slip_Purchased_ID'
          AND object_id = OBJECT_ID(N'dbo.MaterailsRequisition_Slip')
    )
        EXEC(N'CREATE INDEX IX_MaterailsRequisition_Slip_Purchased_ID ON dbo.MaterailsRequisition_Slip (Purchased_ID);');
END;
GO
