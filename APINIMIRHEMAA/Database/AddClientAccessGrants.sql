
IF OBJECT_ID(N'dbo.ClientAccessGrants', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClientAccessGrants
    (
        Client_ID int NOT NULL,
        User_ID int NOT NULL,
        GrantedAtUtc datetime2 NOT NULL
            CONSTRAINT DF_ClientAccessGrants_GrantedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ClientAccessGrants PRIMARY KEY (Client_ID, User_ID),
        CONSTRAINT FK_ClientAccessGrants_Clients
            FOREIGN KEY (Client_ID) REFERENCES dbo.Clients(Client_ID) ON DELETE CASCADE,
        CONSTRAINT FK_ClientAccessGrants_Users
            FOREIGN KEY (User_ID) REFERENCES dbo.Users(User_ID) ON DELETE CASCADE
    );
END

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ClientAccessGrants')
      AND name = N'IX_ClientAccessGrants_User_ID'
)
BEGIN
    CREATE INDEX IX_ClientAccessGrants_User_ID
        ON dbo.ClientAccessGrants(User_ID, Client_ID);
END
GO
