SET XACT_ABORT ON;
GO

DECLARE @TableObjectId int = OBJECT_ID(N'dbo.DeliveryFiles', N'U');
DECLARE @ConstraintCount int;
DECLARE @ConstraintName sysname;
DECLARE @Definition nvarchar(max);
DECLARE @Sql nvarchar(max);

IF @TableObjectId IS NULL
    THROW 50001, 'The dbo.DeliveryFiles table was not found.', 1;

SELECT @ConstraintCount = COUNT(*)
FROM sys.check_constraints
WHERE parent_object_id = @TableObjectId
  AND definition LIKE N'%File_Role%';

IF @ConstraintCount <> 1
    THROW 50002, 'Expected one File_Role check constraint on dbo.DeliveryFiles.', 1;

SELECT
    @ConstraintName = name,
    @Definition = definition
FROM sys.check_constraints
WHERE parent_object_id = @TableObjectId
  AND definition LIKE N'%File_Role%';

IF @Definition NOT LIKE N'%ApproverSigned%'
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;

        SET @Sql =
            N'ALTER TABLE dbo.DeliveryFiles DROP CONSTRAINT ' + QUOTENAME(@ConstraintName) + N';' +
            N'ALTER TABLE dbo.DeliveryFiles WITH CHECK ADD CONSTRAINT ' + QUOTENAME(@ConstraintName) +
            N' CHECK (' + @Definition + N' OR [File_Role] = N''ApproverSigned'');' +
            N'ALTER TABLE dbo.DeliveryFiles CHECK CONSTRAINT ' + QUOTENAME(@ConstraintName) + N';';

        EXEC sys.sp_executesql @Sql;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO
