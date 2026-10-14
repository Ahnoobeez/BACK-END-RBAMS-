IF OBJECT_ID(N'dbo.Quotation', N'U') IS NULL
    THROW 50001, 'Required table dbo.Quotation was not found in the current database.', 1;

IF COL_LENGTH(N'dbo.Quotation', N'End_Date') IS NOT NULL
BEGIN
    DECLARE @defaultConstraint sysname;

    SELECT @defaultConstraint = defaultConstraint.name
    FROM sys.default_constraints AS defaultConstraint
    INNER JOIN sys.columns AS columnDefinition
        ON columnDefinition.object_id = defaultConstraint.parent_object_id
        AND columnDefinition.column_id = defaultConstraint.parent_column_id
    WHERE defaultConstraint.parent_object_id = OBJECT_ID(N'dbo.Quotation')
        AND columnDefinition.name = N'End_Date';

    IF @defaultConstraint IS NOT NULL
        EXEC(N'ALTER TABLE [dbo].[Quotation] DROP CONSTRAINT ' + QUOTENAME(@defaultConstraint));

    ALTER TABLE [dbo].[Quotation] DROP COLUMN [End_Date];
END;
