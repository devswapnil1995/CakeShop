/*
    CakeShop — StoreProfiles.HomeDescription schema repair
    Target: the intended CakeShop database only.

    This script is idempotent:
    - If StoreProfiles does not exist, it stops with an error.
    - If HomeDescription already exists, it makes no schema changes.
    - If the column is missing, it adds nvarchar(320) NOT NULL with the
      application default value. Existing rows receive that default.

    Review the selected database in your SQL client before executing.
    This script does not modify __EFMigrationsHistory.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.StoreProfiles', N'U') IS NULL
BEGIN
    THROW 51001, 'dbo.StoreProfiles was not found in the current database. No repair was performed.', 1;
END;

IF COL_LENGTH(N'dbo.StoreProfiles', N'HomeDescription') IS NULL
BEGIN
    ALTER TABLE dbo.StoreProfiles
        ADD HomeDescription nvarchar(320) NOT NULL
            CONSTRAINT DF_StoreProfiles_HomeDescription
            DEFAULT N'Thoughtfully baked, beautifully shared. Discover the care and creativity behind every Sweet Panda creation.';
END;

COMMIT TRANSACTION;

-- Verify the final column definition.
SELECT
    c.name AS ColumnName,
    TYPE_NAME(c.user_type_id) AS DataType,
    c.max_length AS MaxLengthBytes,
    c.is_nullable AS IsNullable,
    dc.definition AS DefaultDefinition
FROM sys.columns AS c
LEFT JOIN sys.default_constraints AS dc
    ON dc.object_id = c.default_object_id
WHERE c.object_id = OBJECT_ID(N'dbo.StoreProfiles')
  AND c.name = N'HomeDescription';
