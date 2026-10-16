IF OBJECT_ID(N'dbo.UserSecurityAnswer', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserSecurityAnswer
    (
        User_ID int NOT NULL,
        Question_ID int NOT NULL,
        AnswerHash nvarchar(100) NOT NULL,
        CONSTRAINT PK_UserSecurityAnswer PRIMARY KEY (User_ID, Question_ID),
        CONSTRAINT CK_UserSecurityAnswer_Question_ID CHECK (Question_ID BETWEEN 1 AND 30),
        CONSTRAINT FK_UserSecurityAnswer_Users_User_ID
            FOREIGN KEY (User_ID) REFERENCES dbo.Users(User_ID)
            ON DELETE CASCADE
    );
END;

IF OBJECT_ID(N'dbo.PasswordRecoveryChallenge', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordRecoveryChallenge
    (
        Challenge_ID uniqueidentifier NOT NULL,
        User_ID int NULL,
        Question1_ID int NOT NULL,
        Question2_ID int NOT NULL,
        Question3_ID int NOT NULL,
        Attempts int NOT NULL CONSTRAINT DF_PasswordRecoveryChallenge_Attempts DEFAULT (0),
        CreatedAt datetime2 NOT NULL,
        ExpiresAt datetime2 NOT NULL,
        VerifiedAt datetime2 NULL,
        ResetAt datetime2 NULL,
        CONSTRAINT PK_PasswordRecoveryChallenge PRIMARY KEY (Challenge_ID),
        CONSTRAINT CK_PasswordRecoveryChallenge_Question_IDs CHECK
        (
            Question1_ID BETWEEN 1 AND 30 AND
            Question2_ID BETWEEN 1 AND 30 AND
            Question3_ID BETWEEN 1 AND 30 AND
            Question1_ID <> Question2_ID AND
            Question1_ID <> Question3_ID AND
            Question2_ID <> Question3_ID
        ),
        CONSTRAINT CK_PasswordRecoveryChallenge_Attempts CHECK (Attempts BETWEEN 0 AND 3),
        CONSTRAINT FK_PasswordRecoveryChallenge_Users_User_ID
            FOREIGN KEY (User_ID) REFERENCES dbo.Users(User_ID)
            ON DELETE SET NULL
    );
    CREATE INDEX IX_PasswordRecoveryChallenge_ExpiresAt
        ON dbo.PasswordRecoveryChallenge(ExpiresAt);
END;
