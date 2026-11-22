-- ============================================================================
-- CoffeeShopDb SQL Server Schema (Generated from EF Core 8 Model)
-- ============================================================================
IF DB_ID('CoffeeShopDb') IS NULL
BEGIN
    CREATE DATABASE [CoffeeShopDb];
END
GO
USE [CoffeeShopDb];
GO

CREATE TABLE [Categories] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [SortOrder] int NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Ingredients] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(150) NOT NULL,
    [Unit] nvarchar(30) NOT NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [MinQuantity] decimal(18,3) NOT NULL,
    [AverageUnitCost] decimal(18,2) NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Ingredients] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Users] (
    [Id] int NOT NULL IDENTITY,
    [FullName] nvarchar(120) NOT NULL,
    [Username] nvarchar(60) NOT NULL,
    [PasswordHash] nvarchar(255) NOT NULL,
    [Role] int NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Vouchers] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(50) NOT NULL,
    [DiscountPercent] decimal(5,2) NOT NULL,
    [MinOrderAmount] decimal(18,2) NOT NULL,
    [Description] nvarchar(255) NULL,
    [IsActive] bit NOT NULL,
    [ExpiresAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Vouchers] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Products] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [CategoryId] int NOT NULL,
    [ImagePath] nvarchar(500) NULL,
    [IsAvailable] bit NOT NULL,
    [IsDeleted] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [InventoryTransactions] (
    [Id] int NOT NULL IDENTITY,
    [IngredientId] int NOT NULL,
    [Type] int NOT NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [TotalCost] decimal(18,2) NOT NULL,
    [UnitCost] decimal(18,2) NOT NULL,
    [Note] nvarchar(500) NULL,
    [ReferenceOrderId] int NULL,
    [UserId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_InventoryTransactions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InventoryTransactions_Ingredients_IngredientId] FOREIGN KEY ([IngredientId]) REFERENCES [Ingredients] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InventoryTransactions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [WorkSchedules] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [ShiftDate] date NOT NULL,
    [ShiftType] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_WorkSchedules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkSchedules_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [WorkSessions] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [StartTime] datetime2 NOT NULL,
    [EndTime] datetime2 NULL,
    [StartCash] decimal(18,2) NOT NULL,
    [EndCash] decimal(18,2) NOT NULL,
    [CashSales] decimal(18,2) NOT NULL,
    [TransferSales] decimal(18,2) NOT NULL,
    [TotalSales] decimal(18,2) NOT NULL,
    [ExpectedCash] decimal(18,2) NOT NULL,
    [CashVariance] decimal(18,2) NOT NULL,
    [Note] nvarchar(500) NULL,
    [Status] int NOT NULL,
    CONSTRAINT [PK_WorkSessions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkSessions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [RecipeItems] (
    [Id] int NOT NULL IDENTITY,
    [ProductId] int NOT NULL,
    [IngredientId] int NOT NULL,
    [QuantityRequired] decimal(18,3) NOT NULL,
    CONSTRAINT [PK_RecipeItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecipeItems_Ingredients_IngredientId] FOREIGN KEY ([IngredientId]) REFERENCES [Ingredients] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RecipeItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Orders] (
    [Id] int NOT NULL IDENTITY,
    [OrderCode] nvarchar(30) NOT NULL,
    [UserId] int NOT NULL,
    [WorkSessionId] int NULL,
    [OrderDate] datetime2 NOT NULL,
    [Subtotal] decimal(18,2) NOT NULL,
    [VoucherCode] nvarchar(50) NULL,
    [DiscountPercent] decimal(5,2) NOT NULL,
    [DiscountAmount] decimal(18,2) NOT NULL,
    [FinalAmount] decimal(18,2) NOT NULL,
    [PaymentMethod] int NOT NULL,
    [Status] int NOT NULL,
    [CancelledByUserId] int NULL,
    [CancelledAt] datetime2 NULL,
    [CancelReason] nvarchar(255) NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Orders_Users_CancelledByUserId] FOREIGN KEY ([CancelledByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Orders_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Orders_WorkSessions_WorkSessionId] FOREIGN KEY ([WorkSessionId]) REFERENCES [WorkSessions] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [OrderItems] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [ProductId] int NOT NULL,
    [ProductNameSnapshot] nvarchar(200) NOT NULL,
    [Quantity] int NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [UnitCost] decimal(18,2) NOT NULL,
    [Note] nvarchar(255) NULL,
    CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrderItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrderItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
);
GO


CREATE UNIQUE INDEX [IX_Categories_Name] ON [Categories] ([Name]);
GO


CREATE UNIQUE INDEX [IX_Ingredients_Name] ON [Ingredients] ([Name]);
GO


CREATE INDEX [IX_InventoryTransactions_IngredientId] ON [InventoryTransactions] ([IngredientId]);
GO


CREATE INDEX [IX_InventoryTransactions_UserId] ON [InventoryTransactions] ([UserId]);
GO


CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems] ([OrderId]);
GO


CREATE INDEX [IX_OrderItems_ProductId] ON [OrderItems] ([ProductId]);
GO


CREATE INDEX [IX_Orders_CancelledByUserId] ON [Orders] ([CancelledByUserId]);
GO


CREATE UNIQUE INDEX [IX_Orders_OrderCode] ON [Orders] ([OrderCode]);
GO


CREATE INDEX [IX_Orders_UserId] ON [Orders] ([UserId]);
GO


CREATE INDEX [IX_Orders_WorkSessionId] ON [Orders] ([WorkSessionId]);
GO


CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
GO


CREATE INDEX [IX_RecipeItems_IngredientId] ON [RecipeItems] ([IngredientId]);
GO


CREATE UNIQUE INDEX [IX_RecipeItems_ProductId_IngredientId] ON [RecipeItems] ([ProductId], [IngredientId]);
GO


CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
GO


CREATE UNIQUE INDEX [IX_Vouchers_Code] ON [Vouchers] ([Code]);
GO


CREATE UNIQUE INDEX [IX_WorkSchedules_UserId_ShiftDate_ShiftType] ON [WorkSchedules] ([UserId], [ShiftDate], [ShiftType]);
GO


CREATE INDEX [IX_WorkSessions_UserId] ON [WorkSessions] ([UserId]);
GO


