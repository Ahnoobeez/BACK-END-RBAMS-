IF OBJECT_ID(N'dbo.UserSignature', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserSignature
    (
        User_ID int NOT NULL,
        SignatureData varbinary(max) NOT NULL,
        UpdatedAt datetime2 NOT NULL,
        CONSTRAINT PK_UserSignature PRIMARY KEY (User_ID),
        CONSTRAINT FK_UserSignature_Users_User_ID
            FOREIGN KEY (User_ID) REFERENCES dbo.Users(User_ID)
            ON DELETE CASCADE
    );
END;
