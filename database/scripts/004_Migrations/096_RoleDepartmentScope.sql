-- 096: Roles.DepartmentId (additive only: one nullable column + FK; no existing data changed).
-- Lets a role be tied to one department. NULL = organization-wide role (offered for every department on
-- Invite user); a value = the role applies only in that department (offered only when that department is picked).
-- Every existing role stays NULL (organization-wide), so current behaviour is unchanged.
-- The app tolerates this column being absent (every role is treated as organization-wide and the Roles screens
-- hide the Department picker), so the code may be deployed before or after this script.
-- 095 is intentionally unused (the live-kit notes mention a "095" Roles.IsActive check that was never a script).
-- Rollback (role department links are lost; every role becomes organization-wide again):
--   IF OBJECT_ID(N'[dbo].[FK_Roles_Department]', N'F') IS NOT NULL ALTER TABLE [dbo].[Roles] DROP CONSTRAINT [FK_Roles_Department];
--   IF COL_LENGTH(N'dbo.Roles', N'DepartmentId') IS NOT NULL ALTER TABLE [dbo].[Roles] DROP COLUMN [DepartmentId];
--   DELETE FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'096_RoleDepartmentScope.sql';

IF COL_LENGTH(N'dbo.Roles', N'DepartmentId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Roles] ADD [DepartmentId] INT NULL;
END
GO

IF OBJECT_ID(N'[dbo].[FK_Roles_Department]', N'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[Roles] WITH CHECK
        ADD CONSTRAINT [FK_Roles_Department] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Department]([Id]);
END
GO