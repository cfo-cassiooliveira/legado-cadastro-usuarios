namespace Legado.Api.Common.Pseudonymization;

/// <summary>
/// Operação que mascara dado pessoal reconhecido pelo formato em texto livre. Registrada na DI com
/// <see cref="PersonalDataTextMasker.Mask"/>, para os testes poderem simular falha (ex.: timeout da expressão).
/// </summary>
/// <param name="text">Texto de origem externa.</param>
/// <returns>O texto com os dados pessoais trocados pela máscara.</returns>
internal delegate string PersonalDataTextMask(string text);
