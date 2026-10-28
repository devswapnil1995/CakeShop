BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027134651_ReconcileStoreProfileModel'
)
BEGIN
    IF OBJECT_ID(N'dbo.StoreCollections', N'U') IS NOT NULL
       AND OBJECT_ID(
           N'dbo.FK_StoreCollections_ClientMaster_ClientId',
           N'F'
       ) IS NULL
    BEGIN
        ALTER TABLE dbo.StoreCollections
        ADD CONSTRAINT FK_StoreCollections_ClientMaster_ClientId
        FOREIGN KEY (ClientId)
        REFERENCES dbo.ClientMaster (Id)
        ON DELETE NO ACTION;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027134651_ReconcileStoreProfileModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261027134651_ReconcileStoreProfileModel', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027150000_UseStandaloneCollectionHighlights'
)
BEGIN
    ALTER TABLE [StoreCollections] ADD [Highlights] nvarchar(4000) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027150000_UseStandaloneCollectionHighlights'
)
BEGIN
    DROP TABLE [StoreCollectionItems];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027150000_UseStandaloneCollectionHighlights'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261027150000_UseStandaloneCollectionHighlights', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028100000_AddStoreProfile'
)
BEGIN
    CREATE TABLE [StoreProfiles] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [Tagline] nvarchar(120) NOT NULL,
        [StoryTitle] nvarchar(180) NOT NULL,
        [HomeDescription] nvarchar(320) NOT NULL DEFAULT N'Thoughtfully baked, beautifully shared. Discover the care and creativity behind every Sweet Panda creation.',
        [Story] nvarchar(3000) NOT NULL,
        [StoryImageUrl] nvarchar(1000) NULL,
        [LogoUrl] nvarchar(1000) NULL,
        [Phone] nvarchar(40) NULL,
        [WhatsAppNumber] nvarchar(40) NULL,
        [Email] nvarchar(254) NULL,
        [Address] nvarchar(500) NULL,
        [OpeningHours] nvarchar(500) NULL,
        [InstagramUrl] nvarchar(500) NULL,
        [FacebookUrl] nvarchar(500) NULL,
        [WebsiteUrl] nvarchar(500) NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_StoreProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StoreProfiles_ClientMaster_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [ClientMaster] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028100000_AddStoreProfile'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StoreProfiles_ClientId] ON [StoreProfiles] ([ClientId]) WHERE [ClientId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028100000_AddStoreProfile'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261028100000_AddStoreProfile', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028120000_AddAboutPageSections'
)
BEGIN
    CREATE TABLE [AboutPageSections] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [Eyebrow] nvarchar(100) NOT NULL,
        [Title] nvarchar(180) NOT NULL,
        [Body] nvarchar(3000) NOT NULL,
        [ImageUrl] nvarchar(1000) NULL,
        [SecondaryImageUrl] nvarchar(1000) NULL,
        [Layout] nvarchar(24) NOT NULL,
        [Animation] nvarchar(24) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsPublished] bit NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AboutPageSections] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AboutPageSections_ClientMaster_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [ClientMaster] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028120000_AddAboutPageSections'
)
BEGIN
    CREATE INDEX [IX_AboutPageSections_ClientId_SortOrder] ON [AboutPageSections] ([ClientId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028120000_AddAboutPageSections'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261028120000_AddAboutPageSections', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028130000_EnsureStoreProfileHomeDescription'
)
BEGIN
    IF OBJECT_ID(N'[dbo].[StoreProfiles]', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.StoreProfiles', N'HomeDescription') IS NULL
    BEGIN
        ALTER TABLE [dbo].[StoreProfiles]
        ADD [HomeDescription] nvarchar(320) NOT NULL
            CONSTRAINT [DF_StoreProfiles_HomeDescription]
            DEFAULT N'Thoughtfully baked, beautifully shared. Discover the care and creativity behind every Sweet Panda creation.';
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028130000_EnsureStoreProfileHomeDescription'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261028130000_EnsureStoreProfileHomeDescription', N'10.0.0');
END;

COMMIT;
GO

