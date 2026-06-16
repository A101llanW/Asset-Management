-- Legacy global unique indexes are skipped once multitenancy (OrganizationId) is present; see 009_DbConstraints.sql
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssetTag' AND object_id = OBJECT_ID('Asset'))
   AND COL_LENGTH('Asset', 'OrganizationId') IS NULL
BEGIN
    CREATE UNIQUE INDEX IX_AssetTag ON [Asset]([AssetTag]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SerialNumber' AND object_id = OBJECT_ID('Asset'))
BEGIN
    CREATE INDEX IX_SerialNumber ON [Asset]([SerialNumber]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Asset_SerialNumber_NotNull' AND object_id = OBJECT_ID('Asset'))
BEGIN
    CREATE UNIQUE INDEX IX_Asset_SerialNumber_NotNull ON [Asset]([SerialNumber]) WHERE [SerialNumber] IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Asset_BarcodeOrQRCode_NotNull' AND object_id = OBJECT_ID('Asset'))
BEGIN
    CREATE UNIQUE INDEX IX_Asset_BarcodeOrQRCode_NotNull ON [Asset]([BarcodeOrQRCode]) WHERE [BarcodeOrQRCode] IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RolePermission_Role_Permission' AND object_id = OBJECT_ID('RolePermission'))
BEGIN
    CREATE UNIQUE INDEX IX_RolePermission_Role_Permission ON [RolePermission]([RoleId], [PermissionId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Permission_Code' AND object_id = OBJECT_ID('Permission'))
BEGIN
    CREATE UNIQUE INDEX IX_Permission_Code ON [Permission]([Code]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Department_Code' AND object_id = OBJECT_ID('Department'))
   AND COL_LENGTH('Department', 'OrganizationId') IS NULL
BEGIN
    CREATE UNIQUE INDEX IX_Department_Code ON [Department]([Code]);
END
GO
