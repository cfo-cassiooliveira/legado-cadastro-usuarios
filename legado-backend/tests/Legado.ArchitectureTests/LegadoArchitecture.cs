using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using Legado.Application.Users;
using Legado.Domain.Users;
using Legado.Infrastructure.Persistence;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Legado.ArchitectureTests;

/// <summary>Arquitetura carregada uma vez só para todas as regras (performance).</summary>
internal static class LegadoArchitecture
{
    public static readonly Architecture Value = new ArchLoader()
        .LoadAssemblies(
            typeof(EmailAddress).Assembly,
            typeof(IRegisterUserHandler).Assembly,
            typeof(LegadoDbContext).Assembly,
            typeof(Program).Assembly)
        .Build();

    public static readonly IObjectProvider<IType> Domain =
        Types().That().ResideInNamespaceOrBelow("Legado.Domain").As("Domain");

    public static readonly IObjectProvider<IType> Application =
        Types().That().ResideInNamespaceOrBelow("Legado.Application").As("Application");

    public static readonly IObjectProvider<IType> Api =
        Types().That().ResideInNamespaceOrBelow("Legado.Api").As("Api");
}
