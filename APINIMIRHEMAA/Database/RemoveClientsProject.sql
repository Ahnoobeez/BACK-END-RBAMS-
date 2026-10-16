-- Run in both Local and Cloud databases before deploying the API.
-- Existing project links are migrated to Client_ID, and the newest business style
-- for each client (highest Project_ID) is moved onto dbo.Clients.
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @DynamicSql nvarchar(max);

    IF OBJECT_ID(N'dbo.Clients', N'U') IS NULL
        THROW 50101, 'Required table dbo.Clients was not found.', 1;

    IF OBJECT_ID(N'dbo.Quotation', N'U') IS NULL
        THROW 50102, 'Required table dbo.Quotation was not found.', 1;

    IF COL_LENGTH(N'dbo.Clients', N'Business_Style') IS NULL
        ALTER TABLE dbo.Clients ADD Business_Style nvarchar(max) NULL;

    IF OBJECT_ID(N'dbo.ClientsProject', N'U') IS NOT NULL
    BEGIN
        SET @DynamicSql = N'
            ;WITH LatestBusinessStyle AS
            (
                SELECT
                    Client_ID,
                    Business_Style,
                    ROW_NUMBER() OVER (PARTITION BY Client_ID ORDER BY Project_ID DESC) AS RowNumber
                FROM dbo.ClientsProject
                WHERE LEN(LTRIM(RTRIM(Business_Style))) > 0
            )
            UPDATE client
            SET Business_Style = source.Business_Style
            FROM dbo.Clients AS client
            INNER JOIN LatestBusinessStyle AS source
                ON source.Client_ID = client.Client_ID
                AND source.RowNumber = 1
            WHERE client.Business_Style IS NULL
                OR LEN(LTRIM(RTRIM(client.Business_Style))) = 0;';
        EXEC sys.sp_executesql @DynamicSql;
    END

    IF COL_LENGTH(N'dbo.Quotation', N'Client_ID') IS NULL
        ALTER TABLE dbo.Quotation ADD Client_ID int NULL;

    IF COL_LENGTH(N'dbo.Quotation', N'Project_ID') IS NOT NULL
    BEGIN
        IF OBJECT_ID(N'dbo.ClientsProject', N'U') IS NULL
        BEGIN
            SET @DynamicSql = N'
                IF EXISTS (SELECT 1 FROM dbo.Quotation WHERE Client_ID IS NULL)
                    THROW 50103, ''Quotation rows still need a project-to-client mapping. Restore dbo.ClientsProject before retrying.'', 1;';
            EXEC sys.sp_executesql @DynamicSql;
        END

        IF OBJECT_ID(N'dbo.ClientsProject', N'U') IS NOT NULL
        BEGIN
            SET @DynamicSql = N'
                UPDATE quotation
                SET Client_ID = project.Client_ID
                FROM dbo.Quotation AS quotation
                INNER JOIN dbo.ClientsProject AS project
                    ON project.Project_ID = quotation.Project_ID
                WHERE quotation.Client_ID IS NULL;

                IF EXISTS
                (
                    SELECT 1
                    FROM dbo.Quotation AS quotation
                    LEFT JOIN dbo.ClientsProject AS project
                        ON project.Project_ID = quotation.Project_ID
                    WHERE quotation.Client_ID IS NULL
                        OR (project.Client_ID IS NOT NULL AND quotation.Client_ID <> project.Client_ID)
                )
                    THROW 50104, ''Quotation project links could not be mapped consistently to clients.'', 1;';
            EXEC sys.sp_executesql @DynamicSql;
        END
    END

    SET @DynamicSql = N'
        IF EXISTS
        (
            SELECT 1
            FROM dbo.Quotation AS quotation
            LEFT JOIN dbo.Clients AS client ON client.Client_ID = quotation.Client_ID
            WHERE quotation.Client_ID IS NULL OR client.Client_ID IS NULL
        )
            THROW 50105, ''Some quotations do not have a valid client. No project data was removed.'', 1;';
    EXEC sys.sp_executesql @DynamicSql;

    IF OBJECT_ID(N'dbo.ClientQuotationReservation', N'U') IS NOT NULL
    BEGIN
        IF COL_LENGTH(N'dbo.ClientQuotationReservation', N'Client_ID') IS NULL
            ALTER TABLE dbo.ClientQuotationReservation ADD Client_ID int NULL;

        IF COL_LENGTH(N'dbo.ClientQuotationReservation', N'Project_ID') IS NOT NULL
            AND OBJECT_ID(N'dbo.ClientsProject', N'U') IS NOT NULL
        BEGIN
            SET @DynamicSql = N'
                UPDATE reservation
                SET Client_ID = project.Client_ID
                FROM dbo.ClientQuotationReservation AS reservation
                INNER JOIN dbo.ClientsProject AS project
                    ON project.Project_ID = reservation.Project_ID
                WHERE reservation.Client_ID IS NULL;

                IF EXISTS
                (
                    SELECT 1
                    FROM dbo.ClientQuotationReservation AS reservation
                    LEFT JOIN dbo.ClientsProject AS project
                        ON project.Project_ID = reservation.Project_ID
                    WHERE reservation.Client_ID IS NULL
                        OR (project.Client_ID IS NOT NULL AND reservation.Client_ID <> project.Client_ID)
                )
                    THROW 50108, ''Quotation reservation project links could not be mapped consistently to clients.'', 1;';
            EXEC sys.sp_executesql @DynamicSql;
        END

        SET @DynamicSql = N'
            IF EXISTS
            (
                SELECT 1
                FROM dbo.ClientQuotationReservation AS reservation
                LEFT JOIN dbo.Clients AS client ON client.Client_ID = reservation.Client_ID
                WHERE reservation.Client_ID IS NULL OR client.Client_ID IS NULL
            )
                THROW 50106, ''Some quotation reservations do not have a valid client. No project data was removed.'', 1;';
        EXEC sys.sp_executesql @DynamicSql;
    END

    IF OBJECT_ID(N'dbo.ClientsProject', N'U') IS NOT NULL
       AND EXISTS
       (
           SELECT 1
           FROM sys.foreign_keys AS foreignKey
           WHERE foreignKey.referenced_object_id = OBJECT_ID(N'dbo.ClientsProject')
             AND foreignKey.parent_object_id NOT IN
                 (OBJECT_ID(N'dbo.Quotation'), OBJECT_ID(N'dbo.ClientQuotationReservation'))
       )
        THROW 50107, 'Another table still references dbo.ClientsProject. Review that relationship before retrying.', 1;

    DECLARE @ForeignKeyTable sysname;
    DECLARE @ForeignKeySchema sysname;
    DECLARE @ForeignKeyName sysname;
    DECLARE foreign_key_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT schema_name(parent.schema_id), parent.name, foreignKey.name
        FROM sys.foreign_keys AS foreignKey
        INNER JOIN sys.tables AS parent ON parent.object_id = foreignKey.parent_object_id
        WHERE foreignKey.referenced_object_id = OBJECT_ID(N'dbo.ClientsProject')
          AND foreignKey.parent_object_id IN
              (OBJECT_ID(N'dbo.Quotation'), OBJECT_ID(N'dbo.ClientQuotationReservation'));

    OPEN foreign_key_cursor;
    FETCH NEXT FROM foreign_key_cursor INTO @ForeignKeySchema, @ForeignKeyTable, @ForeignKeyName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @DynamicSql =
            N'ALTER TABLE ' + QUOTENAME(@ForeignKeySchema) + N'.' + QUOTENAME(@ForeignKeyTable)
            + N' DROP CONSTRAINT ' + QUOTENAME(@ForeignKeyName) + N';';
        EXEC sys.sp_executesql @DynamicSql;
        FETCH NEXT FROM foreign_key_cursor INTO @ForeignKeySchema, @ForeignKeyTable, @ForeignKeyName;
    END
    CLOSE foreign_key_cursor;
    DEALLOCATE foreign_key_cursor;

    DECLARE @TargetSchema sysname;
    DECLARE @TargetTable sysname;
    DECLARE @IndexName sysname;
    DECLARE index_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT schema_name(tableObject.schema_id), tableObject.name, indexObject.name
        FROM sys.indexes AS indexObject
        INNER JOIN sys.tables AS tableObject ON tableObject.object_id = indexObject.object_id
        INNER JOIN sys.index_columns AS indexColumn
            ON indexColumn.object_id = indexObject.object_id
            AND indexColumn.index_id = indexObject.index_id
        INNER JOIN sys.columns AS columnObject
            ON columnObject.object_id = indexColumn.object_id
            AND columnObject.column_id = indexColumn.column_id
        WHERE tableObject.object_id IN
              (OBJECT_ID(N'dbo.Quotation'), OBJECT_ID(N'dbo.ClientQuotationReservation'))
          AND columnObject.name = N'Project_ID'
          AND indexObject.name IS NOT NULL
          AND indexObject.is_primary_key = 0
          AND indexObject.is_unique_constraint = 0;

    OPEN index_cursor;
    FETCH NEXT FROM index_cursor INTO @TargetSchema, @TargetTable, @IndexName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @DynamicSql =
            N'DROP INDEX ' + QUOTENAME(@IndexName)
            + N' ON ' + QUOTENAME(@TargetSchema) + N'.' + QUOTENAME(@TargetTable) + N';';
        EXEC sys.sp_executesql @DynamicSql;
        FETCH NEXT FROM index_cursor INTO @TargetSchema, @TargetTable, @IndexName;
    END
    CLOSE index_cursor;
    DEALLOCATE index_cursor;

    IF COL_LENGTH(N'dbo.Quotation', N'Project_ID') IS NOT NULL
    BEGIN
        SET @DynamicSql = N'ALTER TABLE dbo.Quotation DROP COLUMN Project_ID;';
        EXEC sys.sp_executesql @DynamicSql;
    END

    IF OBJECT_ID(N'dbo.ClientQuotationReservation', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.ClientQuotationReservation', N'Project_ID') IS NOT NULL
    BEGIN
        SET @DynamicSql = N'ALTER TABLE dbo.ClientQuotationReservation DROP COLUMN Project_ID;';
        EXEC sys.sp_executesql @DynamicSql;
    END

    SET @DynamicSql = N'ALTER TABLE dbo.Quotation ALTER COLUMN Client_ID int NOT NULL;';
    EXEC sys.sp_executesql @DynamicSql;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_key_columns AS foreignKeyColumn
        WHERE foreignKeyColumn.parent_object_id = OBJECT_ID(N'dbo.Quotation')
          AND foreignKeyColumn.parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.Quotation'), N'Client_ID', 'ColumnId')
          AND foreignKeyColumn.referenced_object_id = OBJECT_ID(N'dbo.Clients')
    )
    BEGIN
        SET @DynamicSql = N'
            ALTER TABLE dbo.Quotation
                ADD CONSTRAINT FK_Quotation_Clients_Client_ID
                FOREIGN KEY (Client_ID) REFERENCES dbo.Clients (Client_ID);';
        EXEC sys.sp_executesql @DynamicSql;
    END

    IF OBJECT_ID(N'dbo.ClientQuotationReservation', N'U') IS NOT NULL
    BEGIN
        SET @DynamicSql = N'ALTER TABLE dbo.ClientQuotationReservation ALTER COLUMN Client_ID int NOT NULL;';
        EXEC sys.sp_executesql @DynamicSql;

        IF NOT EXISTS
        (
            SELECT 1
            FROM sys.foreign_key_columns AS foreignKeyColumn
            WHERE foreignKeyColumn.parent_object_id = OBJECT_ID(N'dbo.ClientQuotationReservation')
              AND foreignKeyColumn.parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.ClientQuotationReservation'), N'Client_ID', 'ColumnId')
              AND foreignKeyColumn.referenced_object_id = OBJECT_ID(N'dbo.Clients')
        )
        BEGIN
            SET @DynamicSql = N'
                ALTER TABLE dbo.ClientQuotationReservation
                    ADD CONSTRAINT FK_ClientQuotationReservation_Clients_Client_ID
                    FOREIGN KEY (Client_ID) REFERENCES dbo.Clients (Client_ID);';
            EXEC sys.sp_executesql @DynamicSql;
        END
    END

    IF OBJECT_ID(N'dbo.ClientsProject', N'U') IS NOT NULL
        DROP TABLE dbo.ClientsProject;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF CURSOR_STATUS('local', 'foreign_key_cursor') >= 0
        CLOSE foreign_key_cursor;
    IF CURSOR_STATUS('local', 'foreign_key_cursor') >= -1
        DEALLOCATE foreign_key_cursor;
    IF CURSOR_STATUS('local', 'index_cursor') >= 0
        CLOSE index_cursor;
    IF CURSOR_STATUS('local', 'index_cursor') >= -1
        DEALLOCATE index_cursor;
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
