using Legado.Api.Common.Pseudonymization;

namespace Legado.Api.IntegrationTests.Logging;

/// <summary>
/// Reconhecimento pelo formato em texto livre (client-logs): mesmos padrões do estágio 4 do Graylog e máscara do
/// ADR-0002 vinda do <see cref="PersonalDataMasker"/>, sem token.
/// </summary>
public sealed class PersonalDataTextMaskerTests
{
    [Theory]
    [InlineData("para Ana.Souza@Gmail.com agora", "para ana***@gmail agora")]
    [InlineData("/users/ana.souza%40gmail.com", "/users/ana***@gmail")]
    [InlineData("bia@x.com", "b***@x")]
    [InlineData("cnpj 12.345.678/0001-90.", "cnpj 12.3**.***/***1-90.")]
    [InlineData("cpf 123.456.789-09", "cpf ***.456.789-**")]
    [InlineData("cpf 12345678909", "cpf ***.456.789-**")]
    [InlineData("tel +55 (11) 98765-4321", "tel 11*****4321")]
    [InlineData("tel 11 98765-4321", "tel 11*****4321")]
    public void Mask_WithPersonalDataInText_ReplacesItWithAdr0002Mask(string text, string expected) =>
        PersonalDataTextMasker.Mask(text).ShouldBe(expected);

    [Theory]
    [InlineData("HTTP 500 Internal Server Error")]
    [InlineData("at Legado.Api.Users.RegisterUser (main.js:120:15)")]
    [InlineData("pedido 98765-4321")] // Celular sem DDD fica de fora (ADR-0003 do graylog, item 9).
    [InlineData("ana***@gmail e ***.456.789-**")] // Máscara já aplicada não é mascarada de novo.
    public void Mask_WithoutPersonalData_KeepsText(string text) =>
        PersonalDataTextMasker.Mask(text).ShouldBe(text);
}
