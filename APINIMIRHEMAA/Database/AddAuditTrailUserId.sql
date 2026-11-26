IF OBJECT_ID(N'dbo.AuditTrail', N'U') IS NULL
BEGIN
    THROW 50000, 'dbo.AuditTrail must exist before adding its user relationship.', 1;
END;

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    THROW 50001, 'dbo.Users must exist before adding the audit user relationship.', 1;
END;

IF COL_LENGTH(N'dbo.AuditTrail', N'User_ID') IS NULL
BEGIN
    ALTER TABLE dbo.AuditTrail
        ADD User_ID int NULL;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.AuditTrail')
      AND name = N'FK_AuditTrail_Users_User_ID'
)
BEGIN
    ALTER TABLE dbo.AuditTrail WITH CHECK
        ADD CONSTRAINT FK_AuditTrail_Users_User_ID
        FOREIGN KEY (User_ID) REFERENCES dbo.Users(User_ID)
        ON DELETE SET NULL;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.AuditTrail')
      AND name = N'IX_AuditTrail_User_ID'
)
BEGIN
    CREATE INDEX IX_AuditTrail_User_ID
        ON dbo.AuditTrail(User_ID);
END;
GO
