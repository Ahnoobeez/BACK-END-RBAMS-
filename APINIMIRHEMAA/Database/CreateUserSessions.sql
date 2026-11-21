IF OBJECT_ID(N'dbo.UserSessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserSessions
    (
        Session_ID nvarchar(64) NOT NULL
            CONSTRAINT PK_UserSessions PRIMARY KEY,
        User_ID int NOT NULL,
        LastSeenUtc datetime2 NOT NULL,
        CONSTRAINT FK_UserSessions_Users FOREIGN KEY (User_ID)
            REFERENCES dbo.Users(User_ID) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_UserSessions_User_ID_LastSeenUtc'
      AND object_id = OBJECT_ID(N'dbo.UserSessions')
)
    CREATE INDEX IX_UserSessions_User_ID_LastSeenUtc
        ON dbo.UserSessions(User_ID, LastSeenUtc);
GO
