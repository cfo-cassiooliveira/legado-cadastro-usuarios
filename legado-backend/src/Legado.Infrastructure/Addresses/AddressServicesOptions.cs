using System.ComponentModel.DataAnnotations;

namespace Legado.Infrastructure.Addresses;

/// <summary>Endereços dos serviços públicos de CEP (ViaCEP) e municípios (IBGE).</summary>
public sealed class AddressServicesOptions
{
    /// <summary>Seção de configuração (<c>AddressServices</c>).</summary>
    public const string SectionName = "AddressServices";

    /// <summary>URL base do ViaCEP (consulta de CEP).</summary>
    [Required]
    public required Uri ViaCepBaseUrl { get; init; }

    /// <summary>URL base da API de Localidades do IBGE (municípios).</summary>
    [Required]
    public required Uri IbgeBaseUrl { get; init; }

    /// <summary>Tempo de cache da lista de municípios (muda raramente).</summary>
    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan CitiesCacheDuration { get; init; } = TimeSpan.FromHours(24);
}
