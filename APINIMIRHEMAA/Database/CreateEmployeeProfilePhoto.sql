IF OBJECT_ID(N'dbo.EmployeeProfilePhoto', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeProfilePhoto
    (
        Employee_ID int NOT NULL,
        PhotoData varbinary(max) NOT NULL,
        ContentType nvarchar(32) NOT NULL,
        UpdatedAt datetime2 NOT NULL,
        CONSTRAINT PK_EmployeeProfilePhoto PRIMARY KEY (Employee_ID),
        CONSTRAINT FK_EmployeeProfilePhoto_Employee
            FOREIGN KEY (Employee_ID) REFERENCES dbo.Employee(Employee_Id)
            ON DELETE CASCADE
    );
END;
