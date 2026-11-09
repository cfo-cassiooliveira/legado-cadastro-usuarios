namespace Legado.Api.IntegrationTests.Infrastructure;

/// <summary>Compartilha um único container de banco entre as classes de teste (subir SQL Server é caro).</summary>
[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<LegadoApiFactory>
{
    public const string Name = "api";
}
