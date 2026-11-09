using Legado.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Legado.Database;

/// <summary>
/// Usado apenas pelas ferramentas do EF (<c>migrations add</c>/<c>bundle</c>). A connection string real é
/// passada em tempo de execução ao bundle (<c>--connection</c>); o valor padrão aqui não contém senha.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LegadoDbContext>
{
    private const string ConnectionEnvironmentVariable = "LEGADO_DESIGN_CONNECTION";
    private const string DefaultConnection = "Server=localhost,1433;Database=LegadoUsers;Integrated Security=false;TrustServerCertificate=True";

    /// <summary>Cria o contexto apontando as migrations para este assembly (usado só pelas ferramentas do EF).</summary>
    public LegadoDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable) ?? DefaultConnection;
        var options = new DbContextOptionsBuilder<LegadoDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(LegadoDbContext.MigrationsAssembly))
            .Options;

        return new LegadoDbContext(options);
    }
}
