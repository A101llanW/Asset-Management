-- Phase 4: transactional outbox, notification idempotency, webhook delivery
IF OBJECT_ID(N'[OutboxMessage]', N'U') IS NULL
BEGIN
    CREATE TABLE [OutboxMessage] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [OrganizationId] INT NULL,
        [MessageType] NVARCHAR(50) NOT NULL,
        [Payload] NVARCHAR(MAX) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT DF_OutboxMessage_Status DEFAULT(N'Pending'),
        [Attempts] INT NOT NULL CONSTRAINT DF_OutboxMessage_Attempts DEFAULT(0),
        [LastError] NVARCHAR(1000) NULL,
        [CreatedAt] DATETIME NOT NULL CONSTRAINT DF_OutboxMessage_CreatedAt DEFAULT(GETUTCDATE()),
        [UpdatedAt] DATETIME NULL,
        [ProcessedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_OutboxMessage_IsActive DEFAULT(1)
    );
END
GO

IF COL_LENGTH(N'[OutboxMessage]', N'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE [OutboxMessage] ADD [UpdatedAt] DATETIME NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OutboxMessage_Status_CreatedAt' AND object_id = OBJECT_ID(N'[OutboxMessage]'))
BEGIN
    CREATE INDEX IX_OutboxMessage_Status_CreatedAt ON [OutboxMessage]([Status], [CreatedAt]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Notification]') AND name = N'IdempotencyKey')
BEGIN
    ALTER TABLE [Notification] ADD [IdempotencyKey] NVARCHAR(200) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notification_UserId_IdempotencyKey' AND object_id = OBJECT_ID(N'[Notification]'))
BEGIN
    CREATE UNIQUE INDEX IX_Notification_UserId_IdempotencyKey
        ON [Notification]([UserId], [IdempotencyKey])
        WHERE [IdempotencyKey] IS NOT NULL;
END
GO

IF OBJECT_ID(N'[WebhookDelivery]', N'U') IS NULL
BEGIN
    CREATE TABLE [WebhookDelivery] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [OrganizationId] INT NULL,
        [WebhookSubscriptionId] INT NOT NULL,
        [EventType] NVARCHAR(100) NOT NULL,
        [PayloadJson] NVARCHAR(MAX) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT DF_WebhookDelivery_Status DEFAULT(N'Pending'),
        [Attempts] INT NOT NULL CONSTRAINT DF_WebhookDelivery_Attempts DEFAULT(0),
        [NextRetryUtc] DATETIME NULL,
        [LastError] NVARCHAR(1000) NULL,
        [CreatedAt] DATETIME NOT NULL CONSTRAINT DF_WebhookDelivery_CreatedAt DEFAULT(GETUTCDATE()),
        [UpdatedAt] DATETIME NULL,
        [ProcessedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_WebhookDelivery_IsActive DEFAULT(1),
        CONSTRAINT FK_WebhookDelivery_Subscription FOREIGN KEY ([WebhookSubscriptionId]) REFERENCES [WebhookSubscription]([Id])
    );
END
GO

IF COL_LENGTH(N'[WebhookDelivery]', N'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE [WebhookDelivery] ADD [UpdatedAt] DATETIME NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_WebhookDelivery_Status_NextRetryUtc' AND object_id = OBJECT_ID(N'[WebhookDelivery]'))
BEGIN
    CREATE INDEX IX_WebhookDelivery_Status_NextRetryUtc ON [WebhookDelivery]([Status], [NextRetryUtc]);
END
GO
