using Legado.Application.Addresses;
using Legado.Domain.Addresses;
using Legado.Domain.Common;
using Legado.Domain.Users;
using Microsoft.Extensions.Logging;

namespace Legado.Application.Users;

/// <summary>Dados do autocadastro, como enviados pelo formulário (validados pelo handler).</summary>
/// <param name="FullName">Nome completo.</param>
/// <param name="UserName">Login desejado.</param>
/// <param name="Password">Senha em texto puro (hash feito pelo Identity).</param>
/// <param name="Email">E-mail.</param>
/// <param name="MobilePhone">Celular com ou sem máscara.</param>
/// <param name="PostalCode">CEP com ou sem máscara.</param>
/// <param name="Street">Logradouro.</param>
/// <param name="Number">Número do imóvel.</param>
/// <param name="Complement">Complemento opcional.</param>
/// <param name="District">Bairro.</param>
/// <param name="CityIbgeCode">Código IBGE do município escolhido.</param>
/// <param name="State">Sigla da UF.</param>
public sealed record RegisterUserCommand(
    string? FullName,
    string? UserName,
    string? Password,
    string? Email,
    string? MobilePhone,
    string? PostalCode,
    string? Street,
    string? Number,
    string? Complement,
    string? District,
    int CityIbgeCode,
    string? State);

/// <summary>Cadastra um novo usuário validando dados pessoais e endereço (CEP e município) em fontes oficiais.</summary>
public interface IRegisterUserHandler
{
    /// <summary>Valida os dados, confere CEP/cidade/UF nas fontes oficiais e cria a conta; retorna o id do usuário.</summary>
    Task<Result<Guid>> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken);
}

/// <summary>Implementação do caso de uso de autocadastro.</summary>
internal sealed partial class RegisterUserHandler(
    IPostalCodeLookup postalCodeLookup,
    ICityDirectory cityDirectory,
    IUserAccountWriter userAccountWriter,
    ILogger<RegisterUserHandler> logger) : IRegisterUserHandler
{
    /// <summary>
    /// Valida todos os campos, confere endereço nas fontes oficiais e cria a conta com o perfil User.
    /// Sucesso é logado como Information e rejeição (dado inválido/duplicado) como Warning, só com os códigos.
    /// </summary>
    public async Task<Result<Guid>> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await RegisterAsync(command, cancellationToken);
        if (result.IsSuccess)
        {
            LogUserRegistered(result.Value);
        }
        else
        {
            // Só os códigos dos erros (ex.: email.taken), nunca os valores informados (LGPD).
            LogRegistrationRejected(string.Join(",", result.Errors.Select(e => e.Code)));
        }

        return result;
    }

    private async Task<Result<Guid>> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {

        var errors = new List<DomainError>();
        var fullName = Collect(FullName.Create(command.FullName), errors);
        var userName = Collect(UserName.Create(command.UserName), errors);
        var email = Collect(EmailAddress.Create(command.Email), errors);
        var mobilePhone = Collect(MobilePhone.Create(command.MobilePhone), errors);
        var postalCode = Collect(PostalCode.Create(command.PostalCode), errors);
        var state = Collect(StateCode.Create(command.State), errors);

        if (string.IsNullOrEmpty(command.Password))
        {
            errors.Add(new DomainError("password.required", "password", "Password is required."));
        }

        // Só consulta serviços externos quando os dados locais já são válidos (evita chamadas desnecessárias).
        if (errors.Count > 0 || fullName is null || userName is null || email is null || mobilePhone is null || postalCode is null || state is null)
        {
            return Result.Failure<Guid>(errors);
        }

        var cityResult = await ResolveCityAsync(postalCode, state, command.CityIbgeCode, cancellationToken);
        if (!cityResult.IsSuccess)
        {
            return Result.Failure<Guid>(cityResult.Errors);
        }

        var address = Address.Create(postalCode, command.Street, command.Number, command.Complement, command.District, cityResult.Value, state);
        if (!address.IsSuccess)
        {
            return Result.Failure<Guid>(address.Errors);
        }

        var account = new NewUserAccount(userName, fullName, email, mobilePhone, address.Value);
        return await userAccountWriter.CreateAsync(account, command.Password!, cancellationToken); // Senha validada acima.
    }

    /// <summary>Garante que o município pertence à UF e que o CEP é daquele município.</summary>
    private async Task<Result<City>> ResolveCityAsync(PostalCode postalCode, StateCode state, int cityIbgeCode, CancellationToken cancellationToken)
    {
        var cities = await cityDirectory.ListByStateAsync(state, cancellationToken);
        var city = cities.FirstOrDefault(c => c.IbgeCode == cityIbgeCode);
        if (city is null)
        {
            return Result.Failure<City>(AddressErrors.CityNotInState());
        }

        var postalCodeAddress = await postalCodeLookup.FindAsync(postalCode, cancellationToken);
        if (postalCodeAddress is null)
        {
            return Result.Failure<City>(AddressErrors.PostalCodeNotFound(ErrorType.Validation));
        }

        return postalCodeAddress.CityIbgeCode == city.IbgeCode
            ? Result.Success(city)
            : Result.Failure<City>(AddressErrors.PostalCodeCityMismatch());
    }

    private static T? Collect<T>(Result<T> result, List<DomainError> errors) where T : class
    {
        if (result.IsSuccess)
        {
            return result.Value;
        }

        errors.AddRange(result.Errors);
        return null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} registered")]
    private partial void LogUserRegistered(Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "User registration rejected: {ErrorCodes}")]
    private partial void LogRegistrationRejected(string errorCodes);
}
