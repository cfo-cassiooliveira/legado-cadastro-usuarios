-- Cria (ou atualiza a senha) do login usado pela API, com privilégio mínimo:
-- apenas leitura/escrita de dados, sem permissão de DDL (o schema muda só via migrations).
-- Variáveis sqlcmd: $(AppLogin), $(AppPassword), $(DbName).
USE [master];
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(AppLogin)')
    CREATE LOGIN [$(AppLogin)] WITH PASSWORD = N'$(AppPassword)', CHECK_POLICY = ON;
ELSE
    -- Mantém a senha alinhada ao .env quando o container é recriado.
    ALTER LOGIN [$(AppLogin)] WITH PASSWORD = N'$(AppPassword)';
GO
USE [$(DbName)];
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(AppLogin)')
    CREATE USER [$(AppLogin)] FOR LOGIN [$(AppLogin)];
GO
ALTER ROLE db_datareader ADD MEMBER [$(AppLogin)];
ALTER ROLE db_datawriter ADD MEMBER [$(AppLogin)];
GO
