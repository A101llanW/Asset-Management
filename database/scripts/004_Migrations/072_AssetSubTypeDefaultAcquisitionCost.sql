-- Default acquisition cost per asset sub-type (brand/model variant).
IF OBJECT_ID(N'[AssetSubType]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[AssetSubType]', N'DefaultAcquisitionCost') IS NULL
BEGIN
    ALTER TABLE [AssetSubType]
        ADD [DefaultAcquisitionCost] DECIMAL(18,2) NULL;
END
GO
