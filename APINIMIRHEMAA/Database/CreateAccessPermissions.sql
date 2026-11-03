IF OBJECT_ID(N'dbo.AccessPermissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AccessPermissions
    (
        AccessPermission_ID int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_AccessPermissions PRIMARY KEY,
        Role_ID int NULL,
        Department_ID int NULL,
        User_ID int NULL,
        ServiceKey nvarchar(80) NOT NULL,
        ActionKey nvarchar(20) NOT NULL,
        IsAllowed bit NOT NULL,
        CONSTRAINT CK_AccessPermissions_OneScope CHECK
        (
            (Role_ID IS NOT NULL AND Department_ID IS NULL AND User_ID IS NULL) OR
            (Role_ID IS NULL AND Department_ID IS NOT NULL AND User_ID IS NULL) OR
            (Role_ID IS NOT NULL AND Department_ID IS NOT NULL AND User_ID IS NULL) OR
            (Role_ID IS NULL AND Department_ID IS NULL AND User_ID IS NOT NULL)
        ),
        CONSTRAINT FK_AccessPermissions_Roles FOREIGN KEY (Role_ID)
            REFERENCES dbo.Roles(Role_ID) ON DELETE CASCADE,
        CONSTRAINT FK_AccessPermissions_Departments FOREIGN KEY (Department_ID)
            REFERENCES dbo.Departments(Department_ID) ON DELETE CASCADE,
        CONSTRAINT FK_AccessPermissions_Users FOREIGN KEY (User_ID)
            REFERENCES dbo.Users(User_ID) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AccessPermissions_RoleDepartment' AND object_id = OBJECT_ID(N'dbo.AccessPermissions'))
    CREATE UNIQUE INDEX UX_AccessPermissions_RoleDepartment
        ON dbo.AccessPermissions(Role_ID, Department_ID, ServiceKey, ActionKey)
        WHERE Role_ID IS NOT NULL AND Department_ID IS NOT NULL AND User_ID IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AccessPermissions_Role' AND object_id = OBJECT_ID(N'dbo.AccessPermissions'))
    CREATE UNIQUE INDEX UX_AccessPermissions_Role
        ON dbo.AccessPermissions(Role_ID, ServiceKey, ActionKey)
        WHERE Role_ID IS NOT NULL AND Department_ID IS NULL AND User_ID IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AccessPermissions_Department' AND object_id = OBJECT_ID(N'dbo.AccessPermissions'))
    CREATE UNIQUE INDEX UX_AccessPermissions_Department
        ON dbo.AccessPermissions(Department_ID, ServiceKey, ActionKey)
        WHERE Role_ID IS NULL AND Department_ID IS NOT NULL AND User_ID IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AccessPermissions_User' AND object_id = OBJECT_ID(N'dbo.AccessPermissions'))
    CREATE UNIQUE INDEX UX_AccessPermissions_User
        ON dbo.AccessPermissions(User_ID, ServiceKey, ActionKey)
        WHERE Role_ID IS NULL AND Department_ID IS NULL AND User_ID IS NOT NULL;
GO
