using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Legado.ArchitectureTests;

/// <summary>Regra de dependência da Clean Architecture (Constituição Art. III.1): dependências apontam para dentro.</summary>
public sealed class LayerDependencyTests
{
    [Fact]
    public void Domain_ShouldNotDependOnOuterLayersOrFrameworks() =>
        Types().That().Are(LegadoArchitecture.Domain).Should()
            .NotDependOnAny(Types().That().ResideInNamespaceOrBelow("Legado.Application")
                .Or().ResideInNamespaceOrBelow("Legado.Infrastructure")
                .Or().ResideInNamespaceOrBelow("Legado.Api")
                .Or().ResideInNamespaceOrBelow("Microsoft.EntityFrameworkCore")
                .Or().ResideInNamespaceOrBelow("Microsoft.AspNetCore")
                .Or().ResideInNamespaceOrBelow("System.ComponentModel.DataAnnotations"))
            .Because("Domain não depende de nada (Constituição Art. III.1)")
            .Check(LegadoArchitecture.Value);

    [Fact]
    public void Application_ShouldNotDependOnInfrastructureOrWebFrameworks() =>
        Types().That().Are(LegadoArchitecture.Application).Should()
            .NotDependOnAny(Types().That().ResideInNamespaceOrBelow("Legado.Infrastructure")
                .Or().ResideInNamespaceOrBelow("Legado.Api")
                .Or().ResideInNamespaceOrBelow("Microsoft.EntityFrameworkCore")
                .Or().ResideInNamespaceOrBelow("Microsoft.AspNetCore"))
            .Because("Application só conhece portas; EF Core e ASP.NET Core são detalhes das camadas externas")
            .Check(LegadoArchitecture.Value);

    // Endpoints falam com casos de uso (handlers), nunca direto com o banco. O health check do banco
    // (AddDbContextCheck<LegadoDbContext>) é a exceção: é composição de infraestrutura, não endpoint de negócio.
    [Fact]
    public void ApiEndpoints_ShouldNotAccessPersistenceDirectly() =>
        Types().That().Are(LegadoArchitecture.Api).And().DoNotResideInNamespace("Legado.Api.Health").Should()
            .NotDependOnAny(Types().That().ResideInNamespaceOrBelow("Legado.Infrastructure.Persistence")
                .Or().ResideInNamespaceOrBelow("Microsoft.EntityFrameworkCore"))
            .Because("regra de negócio e acesso a dados ficam atrás dos handlers da Application")
            .Check(LegadoArchitecture.Value);
}
