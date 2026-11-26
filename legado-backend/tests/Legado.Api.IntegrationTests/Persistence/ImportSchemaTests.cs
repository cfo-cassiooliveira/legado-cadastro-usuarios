using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Legado.Api.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;

namespace Legado.Api.IntegrationTests.Persistence;

/// <summary>
/// Esquema <c>import</c> do Cadastro (<c>create-import-schema.sql</c>): migrator com DDL e app com DML só nele, nenhum
/// acesso ao dbo e <c>legacy_app</c> com DENY. Executa os scripts reais do migrator contra um SQL Server real.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed partial class ImportSchemaTests(LegadoApiFactory factory)
{
    private const int PermissionDenied = 229;
    private const int ObjectPermissionDenied = 262;
    private const int SchemaAccessDenied = 2760; // CREATE TABLE em esquema sem permissão ALTER
    private const int DropDenied = 3701; // DROP TABLE sem permissão

    [Fact]
    public async Task ImportSchema_GivesMigratorDdlAndAppDmlOnlyInsideImport_AndDeniesLegacyApp()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await ProvisionAsync(ct);

        // Migrator: cria tabela no esquema import, mas não toca no dbo.
        await using (var migrator = await OpenAsync(setup, setup.Migrator, ct))
        {
            await ExecuteAsync(migrator, "CREATE TABLE [import].[Batches] ([Id] int NOT NULL PRIMARY KEY, [Name] nvarchar(50) NULL)", ct);
            await AssertDeniedAsync(migrator, "SELECT TOP 1 1 FROM [dbo].[AspNetUsers]", ct);
            await AssertDeniedAsync(migrator, "CREATE TABLE [dbo].[Intruder] ([Id] int)", ct);
        }

        // Runtime do Cadastro: DML no import, nada de DDL nem de dbo.
        await using (var app = await OpenAsync(setup, setup.CadastroApp, ct))
        {
            await ExecuteAsync(app, "INSERT INTO [import].[Batches] ([Id], [Name]) VALUES (1, N'lote')", ct);
            await ExecuteAsync(app, "UPDATE [import].[Batches] SET [Name] = N'lote 2' WHERE [Id] = 1", ct);
            await ExecuteAsync(app, "SELECT [Name] FROM [import].[Batches]", ct);
            await ExecuteAsync(app, "DELETE FROM [import].[Batches]", ct);
            await AssertDeniedAsync(app, "SELECT TOP 1 1 FROM [dbo].[AspNetUsers]", ct);
            await AssertDeniedAsync(app, "INSERT INTO [dbo].[AspNetRoles] ([Id], [Name]) VALUES (NEWID(), N'x')", ct);
            await AssertDeniedAsync(app, "CREATE TABLE [import].[Rogue] ([Id] int)", ct);
        }

        // API do Legado: continua lendo o dbo, mas o esquema import está negado.
        await using var legacy = await OpenAsync(setup, setup.LegacyApp, ct);
        await ExecuteAsync(legacy, "SELECT TOP 1 1 FROM [dbo].[AspNetUsers]", ct);
        await AssertDeniedAsync(legacy, "SELECT * FROM [import].[Batches]", ct);
        await AssertDeniedAsync(legacy, "INSERT INTO [import].[Batches] ([Id]) VALUES (2)", ct);
    }

    [Fact]
    public async Task ImportSchema_ScriptIsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await ProvisionAsync(ct);

        // Segunda execução (recriação do container) não falha e mantém as permissões.
        await RunScriptAsync(setup.ImportScript, setup.AdminConnection, setup.Variables, ct);

        await using var app = await OpenAsync(setup, setup.CadastroApp, ct);
        await AssertDeniedAsync(app, "SELECT TOP 1 1 FROM [dbo].[AspNetUsers]", ct);
    }

    [Fact]
    public async Task ImportSchema_MigratorAndApp_CannotChangeOrDropLegacyTables()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await ProvisionAsync(ct);

        await using var migrator = await OpenAsync(setup, setup.Migrator, ct);
        await AssertDeniedAsync(migrator, "DROP TABLE [dbo].[AspNetUsers]", ct);
        await AssertDeniedAsync(migrator, "DELETE FROM [dbo].[AspNetUsers]", ct);
        await using var app = await OpenAsync(setup, setup.CadastroApp, ct);
        await AssertDeniedAsync(app, "UPDATE [dbo].[AspNetUsers] SET [Email] = N'x@x.com'", ct);
        await AssertDeniedAsync(app, "DELETE FROM [dbo].[AspNetUsers]", ct);
    }

    [Fact]
    public async Task ImportSchema_RerunWithRotatedPassword_AlignsTheLoginAndRejectsTheOldOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await ProvisionAsync(ct);
        var rotated = NewPassword();

        await RunScriptAsync(setup.ImportScript, setup.AdminConnection,
            new Dictionary<string, string>(setup.Variables) { ["CadastroAppPassword"] = rotated }, ct);

        await using (await OpenAsync(setup, setup.CadastroApp with { Password = rotated }, ct))
        {
        }

        await Should.ThrowAsync<SqlException>(() => OpenAsync(setup, setup.CadastroApp, ct));
    }

    private async Task<Setup> ProvisionAsync(CancellationToken ct)
    {
        // Banco novo por teste; os logins (nível servidor) levam sufixo único para os testes não colidirem.
        var adminConnection = await factory.CreateEmptyDatabaseAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var database = new SqlConnectionStringBuilder(adminConnection).InitialCatalog;
        var migrator = new Login($"cadastro_migrator_{suffix}", NewPassword());
        var cadastroApp = new Login($"cadastro_app_{suffix}", NewPassword());
        var legacyApp = new Login($"legacy_app_{suffix}", NewPassword());

        var variables = new Dictionary<string, string>
        {
            ["DbName"] = database,
            ["MigratorLogin"] = migrator.Name,
            ["MigratorPassword"] = migrator.Password,
            ["CadastroAppLogin"] = cadastroApp.Name,
            ["CadastroAppPassword"] = cadastroApp.Password,
            ["LegacyAppLogin"] = legacyApp.Name,
        };
        var appLoginScript = ReadScript("create-app-login.sql");
        var importScript = ReadScript("create-import-schema.sql");
        await RunScriptAsync(appLoginScript, adminConnection, new Dictionary<string, string>
        {
            ["AppLogin"] = legacyApp.Name,
            ["AppPassword"] = legacyApp.Password,
            ["DbName"] = database,
        }, ct);
        await RunScriptAsync(importScript, adminConnection, variables, ct);
        return new Setup(adminConnection, importScript, variables, migrator, cadastroApp, legacyApp);
    }

    // Senha gerada a cada execução (nenhum segredo versionado); sufixo cumpre a política de complexidade.
    private static string NewPassword() => $"Aa{Convert.ToHexString(RandomNumberGenerator.GetBytes(12))}#1";

    private static string ReadScript(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "legado-database", "scripts", name);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Script {name} not found from {AppContext.BaseDirectory}.");
    }

    // Reproduz o sqlcmd: troca $(Var) e executa cada lote separado por GO.
    private static async Task RunScriptAsync(string script, string connectionString, Dictionary<string, string> variables, CancellationToken ct)
    {
        var text = VariablePattern().Replace(script, m => variables[m.Groups[1].Value]);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        foreach (var batch in GoPattern().Split(text).Where(b => !string.IsNullOrWhiteSpace(b)))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task<SqlConnection> OpenAsync(Setup setup, Login login, CancellationToken ct)
    {
        var builder = new SqlConnectionStringBuilder(setup.AdminConnection)
        {
            UserID = login.Name,
            Password = login.Password,
            Pooling = false,
        };
        var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
        }
    }

    private static async Task AssertDeniedAsync(SqlConnection connection, string sql, CancellationToken ct)
    {
        var ex = await Should.ThrowAsync<SqlException>(() => ExecuteAsync(connection, sql, ct));
        ex.Number.ShouldBeOneOf(PermissionDenied, ObjectPermissionDenied, 297, 300, SchemaAccessDenied, DropDenied);
    }

    [GeneratedRegex(@"\$\((\w+)\)")]
    private static partial Regex VariablePattern();

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoPattern();

    private sealed record Login(string Name, string Password);

    private sealed record Setup(
        string AdminConnection, string ImportScript, Dictionary<string, string> Variables,
        Login Migrator, Login CadastroApp, Login LegacyApp);
}
