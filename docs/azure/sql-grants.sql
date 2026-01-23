-- Azure SQL: Entra ID / Managed Identity permissions
--
-- Run this while connected as the Azure SQL Server Entra admin.
-- Execute inside the target user database (NOT master).
--
-- Replace the placeholders:
--   <principal_display_name> with:
--     - your Entra user display name/UPN, or
--     - the Web App Managed Identity name (same as the Web App name by default)

-- Create user mapped to Entra principal (user/group/managed identity)
CREATE USER [<principal_display_name>] FROM EXTERNAL PROVIDER;
GO

-- Minimum app permissions (read/write)
ALTER ROLE db_datareader ADD MEMBER [<principal_display_name>];
ALTER ROLE db_datawriter ADD MEMBER [<principal_display_name>];
GO

-- For dev/test where the app runs EF migrations at startup:
-- (remove later if you move migrations to a pipeline step)
ALTER ROLE db_ddladmin ADD MEMBER [<principal_display_name>];
GO

