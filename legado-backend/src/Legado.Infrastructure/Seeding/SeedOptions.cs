namespace Legado.Infrastructure.Seeding;

/// <summary>Massa de dados de demonstração (usuários a serem migrados para o Keycloak).</summary>
public sealed class SeedOptions
{
    /// <summary>Seção de configuração (<c>Seed</c>).</summary>
    public const string SectionName = "Seed";

    /// <summary>Habilita a criação da massa de demonstração.</summary>
    public bool Enabled { get; init; }

    /// <summary>Senha dos usuários de demonstração; obrigatória quando <see cref="Enabled"/>. Nunca versionada.</summary>
    public string? DefaultPassword { get; init; }
}
