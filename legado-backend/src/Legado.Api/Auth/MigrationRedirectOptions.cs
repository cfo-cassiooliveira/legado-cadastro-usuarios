using System.ComponentModel.DataAnnotations;

namespace Legado.Api.Auth;

/// <summary>
/// Destino de quem já foi migrado para o Keycloak (seção <c>Migration</c>, ADR-0005): o login de conta migrada devolve
/// este endereço e a tela redireciona para o Cadastro. Sem valor, a tela só mostra o aviso.
/// </summary>
public sealed class MigrationRedirectOptions : IValidatableObject
{
    /// <summary>Seção de configuração (<c>Migration</c>).</summary>
    public const string SectionName = "Migration";

    /// <summary>Endereço do portal Cadastro.</summary>
    public Uri? NewSystemUrl { get; init; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Só endereço absoluto http(s): o valor vira destino de redirecionamento na tela (evita javascript: e afins).
        if (NewSystemUrl is not null && (!NewSystemUrl.IsAbsoluteUri || NewSystemUrl.Scheme is not ("https" or "http")))
        {
            yield return new ValidationResult("Migration:NewSystemUrl must be an absolute http(s) URL.", [nameof(NewSystemUrl)]);
        }
    }
}
