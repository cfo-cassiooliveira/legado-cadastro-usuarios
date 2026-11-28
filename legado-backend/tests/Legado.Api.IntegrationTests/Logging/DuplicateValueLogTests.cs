using System.Globalization;
using Legado.Api.Common;
using Legado.Api.IntegrationTests.Infrastructure;
using Legado.Application.Users;
using Legado.Domain.Addresses;
using Legado.Domain.Users;
using Legado.Infrastructure.Persistence;
using Legado.Infrastructure.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Serilog.Events;

namespace Legado.Api.IntegrationTests.Logging;

/// <summary>
/// O SQL Server põe o valor duplicado na mensagem de erro. Nenhum log pode levá-lo: nem o do próprio EF Core, nem o do
/// tratador global (Constituição, Art. IV.6). Os testes olham todos os eventos reais da API, não só o do escritor.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class DuplicateValueLogTests(LegadoApiFactory factory)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Register_LosingTheRaceOnAUniqueIndex_NeverLogsTheDuplicatedValue(bool duplicateEmail)
    {
        var ct = TestContext.Current.CancellationToken;
        var marker = Guid.NewGuid().ToString("N")[..8];
        var userName = $"dono{marker}";
        var email = $"corrida.{marker}@exemplo.com";
        await CreateAsync(NewAccount(userName, email), ct);

        var result = await CreateAsync(
            duplicateEmail ? NewAccount($"outro{marker}", email) : NewAccount(userName, $"outro.{marker}@exemplo.com"), ct);

        result.ShouldBeFalse();
        EventsContaining(duplicateEmail ? email : userName).ShouldBeEmpty();
    }

    [Fact]
    public async Task GlobalHandler_ForAnUnexpectedUniqueViolation_LogsTheErrorWithoutTheDuplicatedValue()
    {
        var ct = TestContext.Current.CancellationToken;
        var marker = Guid.NewGuid().ToString("N")[..8];
        var email = $"imprevisto.{marker}@exemplo.com";
        await CreateAsync(NewAccount($"base{marker}", email), ct);
        var exception = await Should.ThrowAsync<DbUpdateException>(async () =>
        {
            // Gravação sem o escritor, que trataria a falha: simula um caminho que a deixa subir até o tratador global.
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LegadoDbContext>();
            var other = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email, ct);
            other.Id = Guid.NewGuid();
            other.UserName = $"clone{marker}";
            other.NormalizedUserName = other.UserName.ToUpperInvariant();
            db.Users.Add(other);
            await db.SaveChangesAsync(ct);
        });
        exception.ToString().ShouldContain(email, Case.Insensitive); // a premissa: a exceção carrega o valor
        var logger = new FakeLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(Substitute.For<IProblemDetailsService>(), logger);
        var context = new DefaultHttpContext();

        await handler.TryHandleAsync(context, exception, ct);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Error);
        record.Exception.ShouldBeNull();
        record.Message.ShouldContain("2601");
        record.Message.ShouldNotContain(email, Case.Insensitive);
        record.Message.ShouldContain(nameof(DbUpdateException));
        EventsContaining(email).ShouldBeEmpty();
    }

    private async Task<bool> CreateAsync(NewUserAccount account, CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        // Simula a corrida: sem os validadores do Identity, só o índice único do banco impede a duplicidade.
        userManager.UserValidators.Clear();
        var writer = new UserAccountWriter(userManager, scope.ServiceProvider.GetRequiredService<LegadoDbContext>(), new FakeLogger<UserAccountWriter>());
        return (await writer.CreateAsync(account, TestUsers.Password, cancellationToken)).IsSuccess;
    }

    private IEnumerable<string> EventsContaining(string value) =>
        factory.Logs.Events.Where(e => Contains(e, value)).Select(e => e.MessageTemplate.Text);

    private static bool Contains(LogEvent logEvent, string value) =>
        logEvent.RenderMessage(CultureInfo.InvariantCulture).Contains(value, StringComparison.OrdinalIgnoreCase)
        || logEvent.Exception?.ToString().Contains(value, StringComparison.OrdinalIgnoreCase) == true
        || logEvent.Properties.Values.Any(p => p.ToString().Contains(value, StringComparison.OrdinalIgnoreCase));

    private static NewUserAccount NewAccount(string userName, string email) => new(
        UserName.Create(userName).Value,
        FullName.Create("Pessoa Sintetica").Value,
        EmailAddress.Create(email).Value,
        MobilePhone.Create("16991234567").Value,
        Address.Create(PostalCode.Create("01001000").Value, "Rua A", "1", null, "Centro",
            new City(FakePostalCodeLookup.SaoPauloIbgeCode, "São Paulo"), StateCode.Create("SP").Value).Value);
}
