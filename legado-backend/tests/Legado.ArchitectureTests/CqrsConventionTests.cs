using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Legado.ArchitectureTests;

/// <summary>Convenções de CQRS leve e nomenclatura (architecture.md).</summary>
public sealed class CqrsConventionTests
{
    private static readonly IObjectProvider<IType> WritePorts =
        Types().That().HaveNameEndingWith("Writer").Or().HaveNameEndingWith("Repository").As("Portas de escrita");

    [Fact]
    public void Handlers_ShouldBeSealed() =>
        Classes().That().Are(LegadoArchitecture.Application).And().HaveNameEndingWith("Handler")
            .Should().BeSealed()
            .Check(LegadoArchitecture.Value);

    [Fact]
    public void Commands_ShouldBeSealed() =>
        Classes().That().Are(LegadoArchitecture.Application).And().HaveNameEndingWith("Command")
            .Should().BeSealed()
            .Check(LegadoArchitecture.Value);

    [Fact]
    public void QueryHandlers_ShouldNotDependOnWritePorts() =>
        Classes().That().Are(LegadoArchitecture.Application).And().HaveNameMatching("^(Get|List|Search).*Handler$")
            .Should().NotDependOnAny(WritePorts)
            .Because("query não altera estado (CQRS leve)")
            .Check(LegadoArchitecture.Value);

    [Fact]
    public void Classes_ShouldNotBeNamedHelperOrManager() =>
        Classes().That().ResideInNamespaceOrBelow("Legado")
            .Should().NotHaveNameEndingWith("Helper").AndShould().NotHaveNameEndingWith("Manager")
            .Because("nomes genéricos escondem responsabilidades misturadas (SRP, architecture.md)")
            .Check(LegadoArchitecture.Value);
}
