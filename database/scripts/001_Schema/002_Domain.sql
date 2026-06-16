-- Core domain tables (singular names match EF model)
IF OBJECT_ID(N'[Roles]', N'U') IS NULL
BEGIN
    CREATE TABLE [Roles] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [IsSystemRole] BIT NOT NULL CONSTRAINT DF_Roles_IsSystemRole DEFAULT(0),
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_Roles_IsActive DEFAULT(1)
    );
END
GO

IF OBJECT_ID(N'[Permission]', N'U') IS NULL
BEGIN
    CREATE TABLE [Permission] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(200) NOT NULL,
        [Code] NVARCHAR(120) NOT NULL,
        [Module] NVARCHAR(100) NULL,
        [Description] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_Permission_IsActive DEFAULT(1)
    );
END
GO

IF OBJECT_ID(N'[RolePermission]', N'U') IS NULL
BEGIN
    CREATE TABLE [RolePermission] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [RoleId] INT NOT NULL,
        [PermissionId] INT NOT NULL,
        CONSTRAINT FK_RolePermission_Role FOREIGN KEY ([RoleId]) REFERENCES [Roles]([Id]) ON DELETE CASCADE,
        CONSTRAINT FK_RolePermission_Permission FOREIGN KEY ([PermissionId]) REFERENCES [Permission]([Id]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'[Department]', N'U') IS NULL
BEGIN
    CREATE TABLE [Department] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(200) NOT NULL,
        [Code] NVARCHAR(40) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_Department_IsActive DEFAULT(1)
    );
END
GO

IF OBJECT_ID(N'[Supplier]', N'U') IS NULL
BEGIN
    CREATE TABLE [Supplier] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [SupplierName] NVARCHAR(200) NOT NULL,
        [ContactPerson] NVARCHAR(200) NULL,
        [Email] NVARCHAR(200) NULL,
        [Phone] NVARCHAR(50) NULL,
        [Address] NVARCHAR(500) NULL,
        [RegistrationNumber] NVARCHAR(100) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_Supplier_IsActive DEFAULT(1)
    );
END
GO

IF OBJECT_ID(N'[AssetCategory]', N'U') IS NULL
BEGIN
    CREATE TABLE [AssetCategory] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [DefaultUsefulLifeMonths] INT NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_AssetCategory_IsActive DEFAULT(1)
    );
END
GO

IF OBJECT_ID(N'[AssetType]', N'U') IS NULL
BEGIN
    CREATE TABLE [AssetType] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AssetCategoryId] INT NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [UsefulLifeMonths] INT NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_AssetType_IsActive DEFAULT(1),
        CONSTRAINT FK_AssetType_AssetCategory FOREIGN KEY ([AssetCategoryId]) REFERENCES [AssetCategory]([Id])
    );
END
GO

IF OBJECT_ID(N'[Asset]', N'U') IS NULL
BEGIN
    CREATE TABLE [Asset] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AssetName] NVARCHAR(200) NOT NULL,
        [AssetTag] NVARCHAR(60) NOT NULL,
        [CategoryId] INT NOT NULL,
        [AssetTypeId] INT NOT NULL,
        [Brand] NVARCHAR(100) NULL,
        [Model] NVARCHAR(100) NULL,
        [SerialNumber] NVARCHAR(120) NULL,
        [BarcodeOrQRCode] NVARCHAR(120) NULL,
        [Specifications] NVARCHAR(MAX) NULL,
        [Condition] INT NOT NULL,
        [CurrentStatus] INT NOT NULL,
        [Description] NVARCHAR(MAX) NULL,
        [PurchaseDate] DATETIME NOT NULL,
        [AcquisitionCost] DECIMAL(18,2) NOT NULL,
        [TaxAmount] DECIMAL(18,2) NOT NULL,
        [Currency] NVARCHAR(10) NULL,
        [SupplierId] INT NOT NULL,
        [DepartmentId] INT NOT NULL,
        [CurrentCustodianId] NVARCHAR(128) NULL,
        [ConditionOnReceipt] NVARCHAR(200) NULL,
        [UsefulLifeMonths] INT NULL,
        [SalvageValue] DECIMAL(18,2) NOT NULL,
        [DepreciationMethod] INT NOT NULL,
        [DepreciationStartDate] DATETIME NOT NULL,
        [CurrentBookValue] DECIMAL(18,2) NOT NULL,
        [AccumulatedDepreciation] DECIMAL(18,2) NOT NULL,
        [ImpairmentNotes] NVARCHAR(MAX) NULL,
        [WarrantyStartDate] DATETIME NULL,
        [WarrantyEndDate] DATETIME NULL,
        [PolicyReference] NVARCHAR(100) NULL,
        [InsuredValue] DECIMAL(18,2) NULL,
        [ImagePath] NVARCHAR(500) NULL,
        [IsLeased] BIT NOT NULL CONSTRAINT DF_Asset_IsLeased DEFAULT(0),
        [IsInsured] BIT NOT NULL CONSTRAINT DF_Asset_IsInsured DEFAULT(0),
        [RowVersion] ROWVERSION NOT NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_Asset_IsActive DEFAULT(1),
        CONSTRAINT FK_Asset_Category FOREIGN KEY ([CategoryId]) REFERENCES [AssetCategory]([Id]),
        CONSTRAINT FK_Asset_AssetType FOREIGN KEY ([AssetTypeId]) REFERENCES [AssetType]([Id]),
        CONSTRAINT FK_Asset_Supplier FOREIGN KEY ([SupplierId]) REFERENCES [Supplier]([Id]),
        CONSTRAINT FK_Asset_Department FOREIGN KEY ([DepartmentId]) REFERENCES [Department]([Id])
    );
END
GO

IF OBJECT_ID(N'[AssetRequest]', N'U') IS NULL
BEGIN
    CREATE TABLE [AssetRequest] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [RequestedById] NVARCHAR(128) NOT NULL,
        [DepartmentId] INT NULL,
        [CategoryId] INT NULL,
        [RequestedAssetId] INT NULL,
        [RequestedAssetTag] NVARCHAR(60) NULL,
        [Justification] NVARCHAR(MAX) NULL,
        [Status] INT NOT NULL,
        [FulfilledAssetId] INT NULL,
        [ReviewedById] NVARCHAR(128) NULL,
        [ReviewedAt] DATETIME NULL,
        [ReviewNotes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_AssetRequest_IsActive DEFAULT(1),
        CONSTRAINT FK_AssetRequest_Department FOREIGN KEY ([DepartmentId]) REFERENCES [Department]([Id]),
        CONSTRAINT FK_AssetRequest_Category FOREIGN KEY ([CategoryId]) REFERENCES [AssetCategory]([Id]),
        CONSTRAINT FK_AssetRequest_RequestedAsset FOREIGN KEY ([RequestedAssetId]) REFERENCES [Asset]([Id]),
        CONSTRAINT FK_AssetRequest_FulfilledAsset FOREIGN KEY ([FulfilledAssetId]) REFERENCES [Asset]([Id])
    );
END
GO

IF OBJECT_ID(N'[AssetDocument]', N'U') IS NULL
BEGIN
    CREATE TABLE [AssetDocument] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AssetId] INT NOT NULL,
        [DocumentType] NVARCHAR(100) NULL,
        [FileName] NVARCHAR(260) NULL,
        [FilePath] NVARCHAR(500) NULL,
        [ContentType] NVARCHAR(100) NULL,
        [FileSizeBytes] BIGINT NOT NULL,
        [UploadedById] NVARCHAR(128) NULL,
        [UploadedAt] DATETIME NOT NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_AssetDocument_IsActive DEFAULT(1),
        CONSTRAINT FK_AssetDocument_Asset FOREIGN KEY ([AssetId]) REFERENCES [Asset]([Id])
    );
END
GO

IF OBJECT_ID(N'[PurchaseRequest]', N'U') IS NULL
BEGIN
    CREATE TABLE [PurchaseRequest] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [RequestNumber] NVARCHAR(50) NULL,
        [RequestedById] NVARCHAR(128) NULL,
        [ApprovedById] NVARCHAR(128) NULL,
        [ApprovalStatus] INT NOT NULL,
        [CurrentApprovalStage] INT NOT NULL,
        [ApprovalStageRoleIds] NVARCHAR(200) NULL,
        [DepartmentId] INT NOT NULL,
        [Justification] NVARCHAR(MAX) NULL,
        [EstimatedUnitCost] DECIMAL(18,2) NOT NULL,
        [Quantity] INT NOT NULL,
        [Currency] NVARCHAR(10) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [ApprovedAt] DATETIME NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_PurchaseRequest_IsActive DEFAULT(1),
        CONSTRAINT FK_PurchaseRequest_Department FOREIGN KEY ([DepartmentId]) REFERENCES [Department]([Id])
    );
END
GO

IF OBJECT_ID(N'[PurchaseApprovalAction]', N'U') IS NULL
BEGIN
    CREATE TABLE [PurchaseApprovalAction] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [PurchaseRequestId] INT NOT NULL,
        [StageNumber] INT NOT NULL,
        [RoleId] INT NOT NULL,
        [ApproverUserId] NVARCHAR(128) NULL,
        [Decision] INT NOT NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [DecisionDate] DATETIME NOT NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_PurchaseApprovalAction_IsActive DEFAULT(1),
        CONSTRAINT FK_PurchaseApprovalAction_PurchaseRequest FOREIGN KEY ([PurchaseRequestId]) REFERENCES [PurchaseRequest]([Id])
    );
END
GO

IF OBJECT_ID(N'[PurchaseRecord]', N'U') IS NULL
BEGIN
    CREATE TABLE [PurchaseRecord] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [PurchaseRequestId] INT NULL,
        [PurchaseOrderNumber] NVARCHAR(50) NULL,
        [SupplierId] INT NOT NULL,
        [InvoiceNumber] NVARCHAR(50) NULL,
        [ReceiptNumber] NVARCHAR(50) NULL,
        [PurchaseDate] DATETIME NOT NULL,
        [DeliveryDate] DATETIME NULL,
        [ReceivedDate] DATETIME NULL,
        [Quantity] INT NOT NULL,
        [UnitCost] DECIMAL(18,2) NOT NULL,
        [TotalCost] DECIMAL(18,2) NOT NULL,
        [Currency] NVARCHAR(10) NULL,
        [TaxAmount] DECIMAL(18,2) NOT NULL,
        [FundingSource] NVARCHAR(100) NULL,
        [BudgetCode] NVARCHAR(50) NULL,
        [UsefulLifeMonths] INT NOT NULL,
        [WarrantyStartDate] DATETIME NULL,
        [WarrantyEndDate] DATETIME NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [RowVersion] ROWVERSION NOT NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_PurchaseRecord_IsActive DEFAULT(1),
        CONSTRAINT FK_PurchaseRecord_Supplier FOREIGN KEY ([SupplierId]) REFERENCES [Supplier]([Id]),
        CONSTRAINT FK_PurchaseRecord_PurchaseRequest FOREIGN KEY ([PurchaseRequestId]) REFERENCES [PurchaseRequest]([Id])
    );
END
GO

IF OBJECT_ID(N'[AssetReceiving]', N'U') IS NULL
BEGIN
    CREATE TABLE [AssetReceiving] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [PurchaseRecordId] INT NOT NULL,
        [AssetId] INT NOT NULL,
        [ReceivedDate] DATETIME NOT NULL,
        [ConditionOnReceipt] NVARCHAR(200) NULL,
        [QuantityReceived] INT NOT NULL,
        [ReceivedById] NVARCHAR(128) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_AssetReceiving_IsActive DEFAULT(1),
        CONSTRAINT FK_AssetReceiving_PurchaseRecord FOREIGN KEY ([PurchaseRecordId]) REFERENCES [PurchaseRecord]([Id]),
        CONSTRAINT FK_AssetReceiving_Asset FOREIGN KEY ([AssetId]) REFERENCES [Asset]([Id])
    );
END
GO

IF OBJECT_ID(N'[AssetAssignment]', N'U') IS NULL
BEGIN
    CREATE TABLE [AssetAssignment] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AssetId] INT NOT NULL,
        [ToDepartmentId] INT NULL,
        [ToUserId] NVARCHAR(128) NULL,
        [AssignmentType] INT NOT NULL,
        [AssignedDate] DATETIME NOT NULL,
        [ExpectedReturnDate] DATETIME NULL,
        [ConditionBeforeHandover] NVARCHAR(200) NULL,
        [AccessoriesHandedOver] NVARCHAR(MAX) NULL,
        [HandoverNotes] NVARCHAR(MAX) NULL,
        [HandedOverById] NVARCHAR(128) NULL,
        [ReceivedById] NVARCHAR(128) NULL,
        [RecipientAcknowledged] BIT NOT NULL CONSTRAINT DF_AssetAssignment_RecipientAcknowledged DEFAULT(0),
        [AcknowledgedAt] DATETIME NULL,
        [RowVersion] ROWVERSION NOT NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_AssetAssignment_IsActive DEFAULT(1),
        CONSTRAINT FK_AssetAssignment_Asset FOREIGN KEY ([AssetId]) REFERENCES [Asset]([Id]),
        CONSTRAINT FK_AssetAssignment_Department FOREIGN KEY ([ToDepartmentId]) REFERENCES [Department]([Id])
    );
END
GO

IF OBJECT_ID(N'[AssetTransfer]', N'U') IS NULL
BEGIN
    CREATE TABLE [AssetTransfer] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AssetId] INT NOT NULL,
        [FromUserId] NVARCHAR(128) NULL,
        [ToUserId] NVARCHAR(128) NULL,
        [FromDepartmentId] INT NULL,
        [ToDepartmentId] INT NULL,
        [Reason] NVARCHAR(MAX) NULL,
        [ConditionBefore] NVARCHAR(200) NULL,
        [ConditionAfter] NVARCHAR(200) NULL,
        [MissingAccessories] BIT NOT NULL CONSTRAINT DF_AssetTransfer_MissingAccessories DEFAULT(0),
        [DamageNotes] NVARCHAR(MAX) NULL,
        [ApprovalStatus] INT NOT NULL,
        [ApprovedById] NVARCHAR(128) NULL,
        [RequestedById] NVARCHAR(128) NULL,
        [CurrentApprovalStage] INT NOT NULL,
        [ApprovalStageRoleIds] NVARCHAR(200) NULL,
        [TransferDate] DATETIME NOT NULL,
        [RowVersion] ROWVERSION NOT NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_AssetTransfer_IsActive DEFAULT(1),
        CONSTRAINT FK_AssetTransfer_Asset FOREIGN KEY ([AssetId]) REFERENCES [Asset]([Id]),
        CONSTRAINT FK_AssetTransfer_FromDepartment FOREIGN KEY ([FromDepartmentId]) REFERENCES [Department]([Id]),
        CONSTRAINT FK_AssetTransfer_ToDepartment FOREIGN KEY ([ToDepartmentId]) REFERENCES [Department]([Id])
    );
END
GO

IF OBJECT_ID(N'[TransferApprovalAction]', N'U') IS NULL
BEGIN
    CREATE TABLE [TransferApprovalAction] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AssetTransferId] INT NOT NULL,
        [StageNumber] INT NOT NULL,
        [RoleId] INT NOT NULL,
        [ApproverUserId] NVARCHAR(128) NULL,
        [Decision] INT NOT NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [DecisionDate] DATETIME NOT NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_TransferApprovalAction_IsActive DEFAULT(1),
        CONSTRAINT FK_TransferApprovalAction_AssetTransfer FOREIGN KEY ([AssetTransferId]) REFERENCES [AssetTransfer]([Id])
    );
END
GO

IF OBJECT_ID(N'[AssetReturn]', N'U') IS NULL
BEGIN
    CREATE TABLE [AssetReturn] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AssetId] INT NOT NULL,
        [ReturnedById] NVARCHAR(128) NULL,
        [ReceivedById] NVARCHAR(128) NULL,
        [ReturnDate] DATETIME NOT NULL,
        [ReturnCondition] NVARCHAR(200) NULL,
        [MissingAccessories] BIT NOT NULL CONSTRAINT DF_AssetReturn_MissingAccessories DEFAULT(0),
        [DamageNotes] NVARCHAR(MAX) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [RowVersion] ROWVERSION NOT NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_AssetReturn_IsActive DEFAULT(1),
        CONSTRAINT FK_AssetReturn_Asset FOREIGN KEY ([AssetId]) REFERENCES [Asset]([Id])
    );
END
GO

IF OBJECT_ID(N'[AssetCustodyEvent]', N'U') IS NULL
BEGIN
    CREATE TABLE [AssetCustodyEvent] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AssetId] INT NOT NULL,
        [ActionType] INT NOT NULL,
        [ActionDate] DATETIME NOT NULL,
        [FromUserId] NVARCHAR(128) NULL,
        [ToUserId] NVARCHAR(128) NULL,
        [FromDepartmentId] INT NULL,
        [ToDepartmentId] INT NULL,
        [ConditionBefore] NVARCHAR(200) NULL,
        [ConditionAfter] NVARCHAR(200) NULL,
        [Reason] NVARCHAR(MAX) NULL,
        [ApprovedById] NVARCHAR(128) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_AssetCustodyEvent_IsActive DEFAULT(1),
        CONSTRAINT FK_AssetCustodyEvent_Asset FOREIGN KEY ([AssetId]) REFERENCES [Asset]([Id])
    );
END
GO

IF OBJECT_ID(N'[AssetMaintenanceRecord]', N'U') IS NULL
BEGIN
    CREATE TABLE [AssetMaintenanceRecord] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [MaintenanceTicketNumber] NVARCHAR(50) NULL,
        [AssetId] INT NOT NULL,
        [ReportedIssue] NVARCHAR(MAX) NULL,
        [MaintenanceType] INT NOT NULL,
        [ReportedById] NVARCHAR(128) NULL,
        [AssignedTechnicianOrVendor] NVARCHAR(200) NULL,
        [ServiceDate] DATETIME NOT NULL,
        [CompletionDate] DATETIME NULL,
        [Cost] DECIMAL(18,2) NOT NULL,
        [Downtime] NVARCHAR(100) NULL,
        [ReplacedParts] NVARCHAR(MAX) NULL,
        [Outcome] NVARCHAR(MAX) NULL,
        [Status] INT NOT NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_AssetMaintenanceRecord_IsActive DEFAULT(1),
        CONSTRAINT FK_AssetMaintenanceRecord_Asset FOREIGN KEY ([AssetId]) REFERENCES [Asset]([Id])
    );
END
GO

IF OBJECT_ID(N'[AssetIncident]', N'U') IS NULL
BEGIN
    CREATE TABLE [AssetIncident] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [IncidentNumber] NVARCHAR(50) NULL,
        [AssetId] INT NOT NULL,
        [ReportedById] NVARCHAR(128) NULL,
        [IncidentType] INT NOT NULL,
        [IncidentDate] DATETIME NOT NULL,
        [Description] NVARCHAR(MAX) NULL,
        [WitnessComments] NVARCHAR(MAX) NULL,
        [PoliceCaseReference] NVARCHAR(100) NULL,
        [Severity] INT NOT NULL,
        [LiabilityNotes] NVARCHAR(MAX) NULL,
        [ResolutionStatus] NVARCHAR(100) NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_AssetIncident_IsActive DEFAULT(1),
        CONSTRAINT FK_AssetIncident_Asset FOREIGN KEY ([AssetId]) REFERENCES [Asset]([Id])
    );
END
GO

IF OBJECT_ID(N'[InsurancePolicy]', N'U') IS NULL
BEGIN
    CREATE TABLE [InsurancePolicy] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AssetId] INT NOT NULL,
        [InsurerName] NVARCHAR(200) NULL,
        [PolicyNumber] NVARCHAR(100) NULL,
        [PolicyStartDate] DATETIME NOT NULL,
        [PolicyEndDate] DATETIME NOT NULL,
        [InsuredValue] DECIMAL(18,2) NOT NULL,
        [ValuationDate] DATETIME NULL,
        [ClaimEligibility] BIT NOT NULL CONSTRAINT DF_InsurancePolicy_ClaimEligibility DEFAULT(0),
        [DeductibleAmount] DECIMAL(18,2) NOT NULL,
        [ClaimNotes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_InsurancePolicy_IsActive DEFAULT(1),
        CONSTRAINT FK_InsurancePolicy_Asset FOREIGN KEY ([AssetId]) REFERENCES [Asset]([Id])
    );
END
GO

IF OBJECT_ID(N'[InsuranceClaim]', N'U') IS NULL
BEGIN
    CREATE TABLE [InsuranceClaim] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ClaimNumber] NVARCHAR(50) NULL,
        [AssetId] INT NOT NULL,
        [IncidentId] INT NULL,
        [ClaimDate] DATETIME NOT NULL,
        [ClaimType] NVARCHAR(100) NULL,
        [Insurer] NVARCHAR(200) NULL,
        [Assessor] NVARCHAR(200) NULL,
        [DocumentsSubmitted] NVARCHAR(MAX) NULL,
        [ClaimStatus] INT NOT NULL,
        [ApprovedAmount] DECIMAL(18,2) NOT NULL,
        [SettlementDate] DATETIME NULL,
        [SettlementNotes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_InsuranceClaim_IsActive DEFAULT(1),
        CONSTRAINT FK_InsuranceClaim_Asset FOREIGN KEY ([AssetId]) REFERENCES [Asset]([Id]),
        CONSTRAINT FK_InsuranceClaim_Incident FOREIGN KEY ([IncidentId]) REFERENCES [AssetIncident]([Id])
    );
END
GO

IF OBJECT_ID(N'[DepreciationRecord]', N'U') IS NULL
BEGIN
    CREATE TABLE [DepreciationRecord] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AssetId] INT NOT NULL,
        [PeriodStartDate] DATETIME NOT NULL,
        [PeriodEndDate] DATETIME NOT NULL,
        [Method] INT NOT NULL,
        [OpeningBookValue] DECIMAL(18,2) NOT NULL,
        [DepreciationAmount] DECIMAL(18,2) NOT NULL,
        [ClosingBookValue] DECIMAL(18,2) NOT NULL,
        [AccumulatedDepreciation] DECIMAL(18,2) NOT NULL,
        [IsPosted] BIT NOT NULL CONSTRAINT DF_DepreciationRecord_IsPosted DEFAULT(0),
        [PostedAt] DATETIME NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_DepreciationRecord_IsActive DEFAULT(1),
        CONSTRAINT FK_DepreciationRecord_Asset FOREIGN KEY ([AssetId]) REFERENCES [Asset]([Id])
    );
END
GO

IF OBJECT_ID(N'[DisposalRecord]', N'U') IS NULL
BEGIN
    CREATE TABLE [DisposalRecord] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AssetId] INT NOT NULL,
        [DisposalRequestDate] DATETIME NOT NULL,
        [DisposalApprovedById] NVARCHAR(128) NULL,
        [DisposalReason] NVARCHAR(MAX) NULL,
        [DisposalMethod] INT NOT NULL,
        [DisposalDate] DATETIME NULL,
        [DisposalAmount] DECIMAL(18,2) NULL,
        [ApprovalStatus] INT NOT NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [RequestedById] NVARCHAR(128) NULL,
        [CurrentApprovalStage] INT NOT NULL,
        [ApprovalStageRoleIds] NVARCHAR(200) NULL,
        [OriginalAssetStatus] INT NOT NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_DisposalRecord_IsActive DEFAULT(1),
        CONSTRAINT FK_DisposalRecord_Asset FOREIGN KEY ([AssetId]) REFERENCES [Asset]([Id])
    );
END
GO

IF OBJECT_ID(N'[DisposalApprovalAction]', N'U') IS NULL
BEGIN
    CREATE TABLE [DisposalApprovalAction] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [DisposalRecordId] INT NOT NULL,
        [StageNumber] INT NOT NULL,
        [RoleId] INT NOT NULL,
        [ApproverUserId] NVARCHAR(128) NULL,
        [Decision] INT NOT NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [DecisionDate] DATETIME NOT NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_DisposalApprovalAction_IsActive DEFAULT(1),
        CONSTRAINT FK_DisposalApprovalAction_DisposalRecord FOREIGN KEY ([DisposalRecordId]) REFERENCES [DisposalRecord]([Id])
    );
END
GO

IF OBJECT_ID(N'[Notification]', N'U') IS NULL
BEGIN
    CREATE TABLE [Notification] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId] NVARCHAR(128) NOT NULL,
        [Type] INT NOT NULL,
        [Subject] NVARCHAR(200) NULL,
        [Message] NVARCHAR(MAX) NULL,
        [Status] INT NOT NULL,
        [ReadAt] DATETIME NULL,
        [LinkUrl] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_Notification_IsActive DEFAULT(1)
    );
END
GO

IF OBJECT_ID(N'[AuditLog]', N'U') IS NULL
BEGIN
    CREATE TABLE [AuditLog] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ActorUserId] NVARCHAR(128) NULL,
        [Action] NVARCHAR(200) NULL,
        [EntityType] NVARCHAR(100) NULL,
        [EntityId] NVARCHAR(100) NULL,
        [OldValues] NVARCHAR(MAX) NULL,
        [NewValues] NVARCHAR(MAX) NULL,
        [Timestamp] DATETIME NOT NULL,
        [IPAddress] NVARCHAR(50) NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_AuditLog_IsActive DEFAULT(1)
    );
END
GO

IF OBJECT_ID(N'[Organization]', N'U') IS NULL
BEGIN
    CREATE TABLE [Organization] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(200) NOT NULL,
        [Code] NVARCHAR(50) NULL,
        [Email] NVARCHAR(200) NULL,
        [Phone] NVARCHAR(50) NULL,
        [Address] NVARCHAR(500) NULL,
        [CurrencyCode] NVARCHAR(10) NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_Organization_IsActive DEFAULT(1)
    );
END
GO

IF OBJECT_ID(N'[SystemSetting]', N'U') IS NULL
BEGIN
    CREATE TABLE [SystemSetting] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [SettingKey] NVARCHAR(100) NOT NULL,
        [SettingValue] NVARCHAR(MAX) NULL,
        [Description] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME NOT NULL,
        [UpdatedAt] DATETIME NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_SystemSetting_IsActive DEFAULT(1)
    );
END
GO
