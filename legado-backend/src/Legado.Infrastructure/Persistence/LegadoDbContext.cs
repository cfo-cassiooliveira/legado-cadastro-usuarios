using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Legado.Infrastructure.Persistence;

/// <summary>Contexto do banco legado: tabelas padrão do Identity com chaves <see cref="Guid"/>.</summary>
public sealed class LegadoDbContext(DbContextOptions<LegadoDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    /// <summary>Assembly onde ficam as migrations (projeto <c>legado-database</c>).</summary>
    public const string MigrationsAssembly = "Legado.Database";

    /// <summary>Aplica o modelo do Identity e as configurações Fluent deste assembly.</summary>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(LegadoDbContext).Assembly);
    }
}
