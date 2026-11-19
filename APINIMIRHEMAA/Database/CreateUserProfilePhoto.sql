IF OBJECT_ID(N'dbo.UserProfilePhoto', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserProfilePhoto
    (
        User_ID int NOT NULL,
        PhotoData varbinary(max) NOT NULL,
        ContentType nvarchar(32) NOT NULL,
        UpdatedAt datetime2 NOT NULL,
        CONSTRAINT PK_UserProfilePhoto PRIMARY KEY (User_ID),
        CONSTRAINT FK_UserProfilePhoto_Users_User_ID
            FOREIGN KEY (User_ID) REFERENCES dbo.Users(User_ID)
            ON DELETE CASCADE
    );
END;
