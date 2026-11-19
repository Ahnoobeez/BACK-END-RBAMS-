IF OBJECT_ID(N'dbo.Notification', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notification
    (
        Notification_ID INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Notification PRIMARY KEY,
        User_ID INT NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Message NVARCHAR(MAX) NOT NULL,
        Type NVARCHAR(50) NOT NULL
            CONSTRAINT DF_Notification_Type DEFAULT (N'General'),
        IsRead BIT NOT NULL
            CONSTRAINT DF_Notification_IsRead DEFAULT (0),
        CreatedAt DATETIME2 NOT NULL
            CONSTRAINT DF_Notification_CreatedAt DEFAULT (SYSUTCDATETIME())
    );

    CREATE INDEX IX_Notification_User_IsRead_CreatedAt
        ON dbo.Notification (User_ID, IsRead, CreatedAt DESC);
END;
