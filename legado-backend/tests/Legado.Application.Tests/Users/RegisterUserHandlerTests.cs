using Legado.Application.Addresses;
using Legado.Application.Users;
using Legado.Domain.Addresses;
using Legado.Domain.Common;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Legado.Application.Tests.Users;

public sealed class RegisterUserHandlerTests
{
    private const int SaoPauloIbgeCode = 3550308;
    private const int CampinasIbgeCode = 3509502;

    private readonly IPostalCodeLookup _postalCodeLookup = Substitute.For<IPostalCodeLookup>();
    private readonly ICityDirectory _cityDirectory = Substitute.For<ICityDirectory>();
    private readonly IUserAccountWriter _userAccountWriter = Substitute.For<IUserAccountWriter>();
    private readonly RegisterUserHandler _handler;

    public RegisterUserHandlerTests()
    {
        _handler = new RegisterUserHandler(_postalCodeLookup, _cityDirectory, _userAccountWriter, NullLogger<RegisterUserHandler>.Instance);

        // Cenário padrão: CEP da Praça da Sé, pertencente a São Paulo/SP.
        _cityDirectory.ListByStateAsync(Arg.Is<StateCode>(s => s.Value == "SP"), Arg.Any<CancellationToken>())
            .Returns([new City(SaoPauloIbgeCode, "São Paulo"), new City(CampinasIbgeCode, "Campinas")]);
        _postalCodeLookup.FindAsync(Arg.Is<PostalCode>(p => p.Value == "01001000"), Arg.Any<CancellationToken>())
            .Returns(new PostalCodeAddress("01001000", "Praça da Sé", "Sé", "São Paulo", SaoPauloIbgeCode, "SP"));
    }

    [Fact]
    public async Task HandleAsync_WithValidData_CreatesAccountWithNormalizedValues()
    {
        var expectedId = Guid.CreateVersion7();
        _userAccountWriter.CreateAsync(Arg.Any<NewUserAccount>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(expectedId));

        var result = await _handler.HandleAsync(ValidCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expectedId);
        await _userAccountWriter.Received(1).CreateAsync(
            Arg.Is<NewUserAccount>(a =>
                a.UserName.Value == "ana.souza"
                && a.Email.Value == "ana@exemplo.com"
                && a.MobilePhone.E164 == "+5516991234567"
                && a.Address.City.IbgeCode == SaoPauloIbgeCode
                && a.Address.PostalCode.Value == "01001000"),
            "Senha@123",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithInvalidFields_ReturnsAllErrorsWithoutCallingExternalServices()
    {
        var command = ValidCommand() with { FullName = "Ana", Email = "invalido", MobilePhone = "123", Password = "" };

        var result = await _handler.HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Errors.Select(e => e.Code).ShouldBe(
            ["full_name.surname_required", "email.invalid", "mobile_phone.invalid", "password.required"], ignoreOrder: true);
        await _postalCodeLookup.DidNotReceiveWithAnyArgs().FindAsync(default!, TestContext.Current.CancellationToken);
        await _cityDirectory.DidNotReceiveWithAnyArgs().ListByStateAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_WhenCityIsNotInState_ReturnsCityError()
    {
        var command = ValidCommand() with { CityIbgeCode = 3304557 };

        var result = await _handler.HandleAsync(command, TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe("city.not_in_state");
        await _userAccountWriter.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_WhenPostalCodeDoesNotExist_ReturnsValidationError()
    {
        var command = ValidCommand() with { PostalCode = "99999-999" };

        var result = await _handler.HandleAsync(command, TestContext.Current.CancellationToken);

        var error = result.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("postal_code.not_found");
        error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task HandleAsync_WhenPostalCodeBelongsToAnotherCity_ReturnsMismatchError()
    {
        var command = ValidCommand() with { CityIbgeCode = CampinasIbgeCode };

        var result = await _handler.HandleAsync(command, TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().Code.ShouldBe("postal_code.city_mismatch");
    }

    [Fact]
    public async Task HandleAsync_WithMissingAddressFields_ReturnsAddressErrors()
    {
        var command = ValidCommand() with { Street = " ", Number = null };

        var result = await _handler.HandleAsync(command, TestContext.Current.CancellationToken);

        result.Errors.Select(e => e.Code).ShouldBe(["street.required", "number.required"], ignoreOrder: true);
    }

    [Fact]
    public async Task HandleAsync_WhenWriterReportsDuplicate_ReturnsConflict()
    {
        var duplicate = new DomainError("user_name.taken", "userName", "User name is already taken.", ErrorType.Conflict);
        _userAccountWriter.CreateAsync(Arg.Any<NewUserAccount>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Guid>(duplicate));

        var result = await _handler.HandleAsync(ValidCommand(), TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().ShouldBe(duplicate);
    }

    [Fact]
    public async Task HandleAsync_WhenAddressServiceIsUnavailable_PropagatesException()
    {
        _cityDirectory.ListByStateAsync(Arg.Any<StateCode>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AddressServiceUnavailableException("down"));

        await Should.ThrowAsync<AddressServiceUnavailableException>(
            () => _handler.HandleAsync(ValidCommand(), TestContext.Current.CancellationToken));
    }

    private static RegisterUserCommand ValidCommand() => new(
        FullName: "Ana Paula Souza",
        UserName: "Ana.Souza",
        Password: "Senha@123",
        Email: "Ana@Exemplo.com",
        MobilePhone: "(16) 99123-4567",
        PostalCode: "01001-000",
        Street: "Praça da Sé",
        Number: "100",
        Complement: null,
        District: "Sé",
        CityIbgeCode: SaoPauloIbgeCode,
        State: "sp");
}
