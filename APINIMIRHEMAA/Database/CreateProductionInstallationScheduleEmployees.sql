IF OBJECT_ID(N'dbo.Installation_Schedule', N'U') IS NULL
    THROW 50000, 'dbo.Installation_Schedule was not found in the current database.', 1;

IF OBJECT_ID(N'dbo.Employee', N'U') IS NULL
    THROW 50001, 'dbo.Employee was not found in the current database.', 1;

IF OBJECT_ID(N'dbo.Installation_Schedule_Employee', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Installation_Schedule_Employee
    (
        Installation_Schedule_Employee_ID int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Installation_Schedule_Employee PRIMARY KEY,
        Installation_ID int NOT NULL,
        Employee_Id int NOT NULL,
        CONSTRAINT FK_Installation_Schedule_Employee_Schedule
            FOREIGN KEY (Installation_ID)
            REFERENCES dbo.Installation_Schedule (Installation_ID)
            ON DELETE CASCADE,
        CONSTRAINT FK_Installation_Schedule_Employee_Employee
            FOREIGN KEY (Employee_Id)
            REFERENCES dbo.Employee (Employee_Id),
        CONSTRAINT UQ_Installation_Schedule_Employee
            UNIQUE (Installation_ID, Employee_Id)
    );
END;
GO
