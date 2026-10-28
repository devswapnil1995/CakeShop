IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114056_InitialCreate'
)
BEGIN
    CREATE TABLE [ClientMaster] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Slug] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ClientMaster] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114056_InitialCreate'
)
BEGIN
    CREATE TABLE [AdminUsers] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [Email] nvarchar(254) NOT NULL,
        [NormalizedEmail] nvarchar(254) NOT NULL,
        [PasswordHash] nvarchar(1000) NOT NULL,
        [Role] nvarchar(40) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AdminUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AdminUsers_ClientMaster_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [ClientMaster] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114056_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Slug] nvarchar(120) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Categories_ClientId_Id] UNIQUE ([ClientId], [Id]),
        CONSTRAINT [FK_Categories_ClientMaster_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [ClientMaster] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114056_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Slug] nvarchar(180) NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [ImageUrl] nvarchar(1000) NULL,
        [Badge] nvarchar(80) NULL,
        [IsAvailable] bit NOT NULL,
        [IsFeatured] bit NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Products_Categories_ClientId_CategoryId] FOREIGN KEY ([ClientId], [CategoryId]) REFERENCES [Categories] ([ClientId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Products_ClientMaster_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [ClientMaster] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114056_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AdminUsers_ClientId_NormalizedEmail] ON [AdminUsers] ([ClientId], [NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114056_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_ClientId_Slug] ON [Categories] ([ClientId], [Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114056_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ClientMaster_Slug] ON [ClientMaster] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114056_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_ClientId_CategoryId] ON [Products] ([ClientId], [CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114056_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_ClientId_Slug] ON [Products] ([ClientId], [Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012114056_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012114056_InitialCreate', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019095238_AddCustomerOrders'
)
BEGIN
    CREATE TABLE [CustomerOrders] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [OrderNumber] nvarchar(40) NOT NULL,
        [CustomerName] nvarchar(120) NOT NULL,
        [CustomerPhone] nvarchar(20) NOT NULL,
        [ProductId] uniqueidentifier NULL,
        [CakeName] nvarchar(160) NOT NULL,
        [Quantity] int NOT NULL,
        [CakeSize] nvarchar(30) NOT NULL,
        [PreferredDate] date NULL,
        [Customization] nvarchar(1000) NULL,
        [Status] nvarchar(30) NOT NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_CustomerOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerOrders_ClientMaster_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [ClientMaster] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019095238_AddCustomerOrders'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomerOrders_ClientId_OrderNumber] ON [CustomerOrders] ([ClientId], [OrderNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019095238_AddCustomerOrders'
)
BEGIN
    CREATE INDEX [IX_CustomerOrders_ClientId_Status_CreatedUtc] ON [CustomerOrders] ([ClientId], [Status], [CreatedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019095238_AddCustomerOrders'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019095238_AddCustomerOrders', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019121000_UpdateOrderDetails'
)
BEGIN
    ALTER TABLE [CustomerOrders] ADD [CompletedUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019121000_UpdateOrderDetails'
)
BEGIN
    ALTER TABLE [CustomerOrders] ADD [TotalAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019121000_UpdateOrderDetails'
)
BEGIN
    ALTER TABLE [CustomerOrders] ADD [UnitPrice] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019121000_UpdateOrderDetails'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019121000_UpdateOrderDetails', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019134306_AddOrderStatusHistory'
)
BEGIN
    ALTER TABLE [CustomerOrders] ADD CONSTRAINT [AK_CustomerOrders_ClientId_Id] UNIQUE ([ClientId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019134306_AddOrderStatusHistory'
)
BEGIN
    CREATE TABLE [OrderStatusHistories] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [PreviousStatus] nvarchar(30) NOT NULL,
        [NewStatus] nvarchar(30) NOT NULL,
        [ChangedUtc] datetime2 NOT NULL,
        [ChangedBy] nvarchar(254) NULL,
        CONSTRAINT [PK_OrderStatusHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderStatusHistories_CustomerOrders_ClientId_OrderId] FOREIGN KEY ([ClientId], [OrderId]) REFERENCES [CustomerOrders] ([ClientId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019134306_AddOrderStatusHistory'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistories_ClientId_OrderId_ChangedUtc] ON [OrderStatusHistories] ([ClientId], [OrderId], [ChangedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019134306_AddOrderStatusHistory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019134306_AddOrderStatusHistory', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019151336_AddExpenses'
)
BEGIN
    CREATE TABLE [Expenses] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [Category] nvarchar(80) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [ExpenseDate] date NOT NULL,
        [Description] nvarchar(500) NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Expenses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Expenses_ClientMaster_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [ClientMaster] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019151336_AddExpenses'
)
BEGIN
    CREATE INDEX [IX_Expenses_ClientId_ExpenseDate] ON [Expenses] ([ClientId], [ExpenseDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019151336_AddExpenses'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019151336_AddExpenses', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020153839_AddProductVariants'
)
BEGIN
    ALTER TABLE [Products] ADD CONSTRAINT [AK_Products_ClientId_Id] UNIQUE ([ClientId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020153839_AddProductVariants'
)
BEGIN
    CREATE TABLE [ProductVariants] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [SizeLabel] nvarchar(30) NOT NULL,
        [WeightInGrams] int NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [IsAvailable] bit NOT NULL,
        CONSTRAINT [PK_ProductVariants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductVariants_Products_ClientId_ProductId] FOREIGN KEY ([ClientId], [ProductId]) REFERENCES [Products] ([ClientId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020153839_AddProductVariants'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductVariants_ClientId_ProductId_WeightInGrams] ON [ProductVariants] ([ClientId], [ProductId], [WeightInGrams]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020153839_AddProductVariants'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020153839_AddProductVariants', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021073205_AddProductVariantToCustomerOrders'
)
BEGIN
    ALTER TABLE [CustomerOrders] ADD [ProductVariantId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021073205_AddProductVariantToCustomerOrders'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261021073205_AddProductVariantToCustomerOrders', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022062654_AddTenantPayments'
)
BEGIN
    CREATE TABLE [PaymentTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [Provider] nvarchar(30) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [ProviderOrderId] nvarchar(100) NULL,
        [ProviderPaymentId] nvarchar(100) NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentTransactions_ClientMaster_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [ClientMaster] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentTransactions_CustomerOrders_ClientId_OrderId] FOREIGN KEY ([ClientId], [OrderId]) REFERENCES [CustomerOrders] ([ClientId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022062654_AddTenantPayments'
)
BEGIN
    CREATE TABLE [TenantPaymentSettings] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [Provider] nvarchar(30) NOT NULL,
        [Environment] nvarchar(10) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [MerchantAccountReference] nvarchar(150) NULL,
        [KeyId] nvarchar(150) NULL,
        [CredentialSecretReference] nvarchar(500) NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_TenantPaymentSettings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantPaymentSettings_ClientMaster_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [ClientMaster] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022062654_AddTenantPayments'
)
BEGIN
    CREATE INDEX [IX_PaymentTransactions_ClientId_OrderId] ON [PaymentTransactions] ([ClientId], [OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022062654_AddTenantPayments'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PaymentTransactions_Provider_ProviderOrderId] ON [PaymentTransactions] ([Provider], [ProviderOrderId]) WHERE [ProviderOrderId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022062654_AddTenantPayments'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PaymentTransactions_Provider_ProviderPaymentId] ON [PaymentTransactions] ([Provider], [ProviderPaymentId]) WHERE [ProviderPaymentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022062654_AddTenantPayments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TenantPaymentSettings_ClientId_Provider_Environment] ON [TenantPaymentSettings] ([ClientId], [Provider], [Environment]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022062654_AddTenantPayments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022062654_AddTenantPayments', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022073138_SyncPaymentWebhookModel'
)
BEGIN
    CREATE TABLE [ProcessedPaymentWebhooks] (
        [Id] uniqueidentifier NOT NULL,
        [Provider] nvarchar(40) NOT NULL,
        [EventId] nvarchar(200) NOT NULL,
        [ProcessedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ProcessedPaymentWebhooks] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022073138_SyncPaymentWebhookModel'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProcessedPaymentWebhooks_Provider_EventId] ON [ProcessedPaymentWebhooks] ([Provider], [EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022073138_SyncPaymentWebhookModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022073138_SyncPaymentWebhookModel', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023120000_AddHeroBanners'
)
BEGIN
    CREATE TABLE [HeroBanners] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [Title] nvarchar(160) NOT NULL,
        [Subtitle] nvarchar(500) NOT NULL,
        [MediaType] nvarchar(20) NOT NULL,
        [MediaUrl] nvarchar(2000) NOT NULL,
        [MobileMediaUrl] nvarchar(2000) NULL,
        [ButtonText] nvarchar(80) NULL,
        [ButtonUrl] nvarchar(2000) NULL,
        [SortOrder] int NOT NULL,
        [IsPublished] bit NOT NULL,
        [StartsUtc] datetime2 NULL,
        [EndsUtc] datetime2 NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_HeroBanners] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HeroBanners_ClientMaster_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [ClientMaster] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023120000_AddHeroBanners'
)
BEGIN
    CREATE INDEX [IX_HeroBanners_ClientId_SortOrder] ON [HeroBanners] ([ClientId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023120000_AddHeroBanners'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261023120000_AddHeroBanners', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024072427_SyncPendingModelChanges'
)
BEGIN
    ALTER TABLE [HeroBanners] ADD [TextPosition] nvarchar(20) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024072427_SyncPendingModelChanges'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261024072427_SyncPendingModelChanges', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026130000_AddCartOrderItems'
)
BEGIN
    ALTER TABLE [ProductVariants] ADD CONSTRAINT [AK_ProductVariants_ClientId_Id] UNIQUE ([ClientId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026130000_AddCartOrderItems'
)
BEGIN
    CREATE TABLE [CustomerOrderItems] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductVariantId] uniqueidentifier NOT NULL,
        [ProductName] nvarchar(160) NOT NULL,
        [SizeLabel] nvarchar(30) NOT NULL,
        [WeightInGrams] int NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_CustomerOrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerOrderItems_CustomerOrders_ClientId_OrderId] FOREIGN KEY ([ClientId], [OrderId]) REFERENCES [CustomerOrders] ([ClientId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomerOrderItems_Products_ClientId_ProductId] FOREIGN KEY ([ClientId], [ProductId]) REFERENCES [Products] ([ClientId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomerOrderItems_ProductVariants_ClientId_ProductVariantId] FOREIGN KEY ([ClientId], [ProductVariantId]) REFERENCES [ProductVariants] ([ClientId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026130000_AddCartOrderItems'
)
BEGIN
    CREATE INDEX [IX_CustomerOrderItems_ClientId_OrderId] ON [CustomerOrderItems] ([ClientId], [OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026130000_AddCartOrderItems'
)
BEGIN
    CREATE INDEX [IX_CustomerOrderItems_ClientId_ProductId] ON [CustomerOrderItems] ([ClientId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026130000_AddCartOrderItems'
)
BEGIN
    CREATE INDEX [IX_CustomerOrderItems_ClientId_ProductVariantId] ON [CustomerOrderItems] ([ClientId], [ProductVariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026130000_AddCartOrderItems'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261026130000_AddCartOrderItems', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027100000_AddStoreCollections'
)
BEGIN
    CREATE TABLE [StoreCollections] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(20) NOT NULL,
        [Title] nvarchar(160) NOT NULL,
        [Slug] nvarchar(180) NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [ImageUrl] nvarchar(1000) NULL,
        [NavigationLabel] nvarchar(60) NULL,
        [ShowInNavigation] bit NOT NULL,
        [IsPublished] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [StartsUtc] datetime2 NULL,
        [EndsUtc] datetime2 NULL,
        [CreatedUtc] datetime2 NOT NULL,
        [UpdatedUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_StoreCollections] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_StoreCollections_ClientId_Id] UNIQUE ([ClientId], [Id]),
        CONSTRAINT [FK_StoreCollections_ClientMaster_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [ClientMaster] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027100000_AddStoreCollections'
)
BEGIN
    CREATE TABLE [StoreCollectionItems] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [CollectionId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_StoreCollectionItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StoreCollectionItems_StoreCollections_ClientId_CollectionId] FOREIGN KEY ([ClientId], [CollectionId]) REFERENCES [StoreCollections] ([ClientId], [Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_StoreCollectionItems_Products_ClientId_ProductId] FOREIGN KEY ([ClientId], [ProductId]) REFERENCES [Products] ([ClientId], [Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027100000_AddStoreCollections'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StoreCollections_ClientId_Kind_Slug] ON [StoreCollections] ([ClientId], [Kind], [Slug]) WHERE [ClientId] IS NOT NULL AND [Kind] IS NOT NULL AND [Slug] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027100000_AddStoreCollections'
)
BEGIN
    CREATE INDEX [IX_StoreCollections_ClientId_IsPublished_SortOrder] ON [StoreCollections] ([ClientId], [IsPublished], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027100000_AddStoreCollections'
)
BEGIN
    CREATE INDEX [IX_StoreCollectionItems_ClientId_CollectionId_SortOrder] ON [StoreCollectionItems] ([ClientId], [CollectionId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027100000_AddStoreCollections'
)
BEGIN
    CREATE INDEX [IX_StoreCollectionItems_ClientId_ProductId] ON [StoreCollectionItems] ([ClientId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027100000_AddStoreCollections'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261027100000_AddStoreCollections', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027130000_AddCollectionBundlePrice'
)
BEGIN
    ALTER TABLE [StoreCollections] ADD [BundlePrice] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027130000_AddCollectionBundlePrice'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261027130000_AddCollectionBundlePrice', N'10.0.0');
END;

COMMIT;
GO

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

