-- Prepara o esquema "import" usado pelo Cadastro (importação de usuários, ADR-0007 do ../cadastro) e os logins dele.
-- Aqui só ficam esquema, logins e permissões; as TABELAS são criadas pelas migrations do Cadastro (login MigratorLogin).
-- Idempotente: pode rodar a cada "compose up". Nenhum dos logins tem acesso ao dbo (as tabelas do Identity).
-- Variáveis sqlcmd: $(DbName), $(MigratorLogin), $(MigratorPassword), $(CadastroAppLogin), $(CadastroAppPassword),
-- $(LegacyAppLogin).
USE [master];
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(MigratorLogin)')
    CREATE LOGIN [$(MigratorLogin)] WITH PASSWORD = N'$(MigratorPassword)', CHECK_POLICY = ON;
ELSE
    -- Mantém a senha alinhada ao .env quando o container é recriado.
    ALTER LOGIN [$(MigratorLogin)] WITH PASSWORD = N'$(MigratorPassword)';
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(CadastroAppLogin)')
    CREATE LOGIN [$(CadastroAppLogin)] WITH PASSWORD = N'$(CadastroAppPassword)', CHECK_POLICY = ON;
ELSE
    ALTER LOGIN [$(CadastroAppLogin)] WITH PASSWORD = N'$(CadastroAppPassword)';
GO
USE [$(DbName)];
GO
IF SCHEMA_ID(N'import') IS NULL
    EXEC (N'CREATE SCHEMA [import] AUTHORIZATION [dbo]');
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(MigratorLogin)')
    CREATE USER [$(MigratorLogin)] FOR LOGIN [$(MigratorLogin)] WITH DEFAULT_SCHEMA = [import];
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(CadastroAppLogin)')
    CREATE USER [$(CadastroAppLogin)] FOR LOGIN [$(CadastroAppLogin)] WITH DEFAULT_SCHEMA = [import];
GO
-- Migrator: DDL só dentro do esquema import (CREATE TABLE no banco + CONTROL no esquema; sem nada no dbo).
GRANT CREATE TABLE TO [$(MigratorLogin)];
GRANT CONTROL ON SCHEMA::[import] TO [$(MigratorLogin)];
-- Runtime do Cadastro: só DML no esquema import.
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[import] TO [$(CadastroAppLogin)];
-- A API do Legado nunca enxerga o esquema import (o DENY vence o db_datareader/db_datawriter).
DENY SELECT, INSERT, UPDATE, DELETE, EXECUTE, ALTER, REFERENCES ON SCHEMA::[import] TO [$(LegacyAppLogin)];
GO
