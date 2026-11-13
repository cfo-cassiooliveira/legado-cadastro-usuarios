using System.Net;
using System.Security.Cryptography;
using Legado.Api.Health;
using Legado.Application.Addresses;
using Legado.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Core;
using Testcontainers.MsSql;

namespace Legado.Api.IntegrationTests.Infrastructure;

/// <summary>Sobe a API contra um SQL Server real (Testcontainers) com as migrations do legado-database.</summary>
public sealed class LegadoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Chave gerada a cada execução: nenhum segredo (nem de teste) fica versionado (Constituição Art. IV.4).
    private static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private const string HighRateLimit = "10000";

    // Mesma imagem (tag + digest) do docker-compose.yml e do migrator: testes reprodutíveis.
    private const string SqlServerImage =
        "mcr.microsoft.com/mssql/server:2025-latest@sha256:05644dad1266ca3d445d1e8f6d2ebb9a6a03df9a39376e0f7b48917356dca9d9";

    private readonly MsSqlContainer _database = new MsSqlBuilder(SqlServerImage).Build();

    public FakePostalCodeLookup PostalCodes { get; } = new();

    /// <summary>Todos os eventos de log emitidos pela API durante os testes.</summary>
    public CollectingLogSink Logs { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync(TestContext.Current.CancellationToken);
        await MigrateAsync(_database.GetConnectionString());
    }

    /// <summary>Cria um banco novo e vazio (já migrado) no mesmo container, para testes que exigem base limpa.</summary>
    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var builder = new SqlConnectionStringBuilder(_database.GetConnectionString())
        {
            InitialCatalog = $"Legado_{Guid.NewGuid():N}",
        };
        await MigrateAsync(builder.ConnectionString);
        return builder.ConnectionString;
    }

    private static async Task MigrateAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<LegadoDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(LegadoDbContext.MigrationsAssembly))
            .Options;
        await using var context = new LegadoDbContext(options);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:LegadoDb", _database.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("RateLimiting:CredentialsPermitsPerMinute", HighRateLimit);
        builder.UseSetting("RateLimiting:AddressLookupPermitsPerMinute", HighRateLimit);
        builder.UseSetting("RateLimiting:ClientLogsPermitsPerMinute", HighRateLimit);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPostalCodeLookup>();
            services.RemoveAll<ICityDirectory>();
            services.AddSingleton<IPostalCodeLookup>(PostalCodes);
            services.AddSingleton<ICityDirectory, FakeCityDirectory>();
            // O Serilog lê sinks do DI (ReadFrom.Services): os testes inspecionam os logs reais.
            services.AddSingleton<ILogEventSink>(Logs);

            // Sondas de health dos serviços externos respondem localmente (sem rede nos testes).
            services.AddHttpClient(HealthChecksSetup.ViaCepProbeClient)
                .ConfigurePrimaryHttpMessageHandler(() => new StaticResponseHandler(HttpStatusCode.OK));
            services.AddHttpClient(HealthChecksSetup.IbgeProbeClient)
                .ConfigurePrimaryHttpMessageHandler(() => new StaticResponseHandler(HttpStatusCode.OK));
        });
    }
}
