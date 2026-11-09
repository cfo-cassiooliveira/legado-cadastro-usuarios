namespace Legado.Application.Addresses;

/// <summary>Endereço retornado pela consulta de CEP (logradouro e bairro podem vir vazios em CEP geral de cidade).</summary>
public sealed record PostalCodeAddress(
    string PostalCode,
    string Street,
    string District,
    string City,
    int CityIbgeCode,
    string State);
