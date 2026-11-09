using Legado.Api.Addresses;
using Legado.Api.Auth;
using Legado.Api.ClientLogs;
using Legado.Api.Common;
using Legado.Api.Health;
using Legado.Api.Users;
using Legado.Application;
using Legado.Infrastructure;
using Legado.Infrastructure.Seeding;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Não revela o servidor/versão no header "Server" (OWASP A02).
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddLegacyLogging(builder.Configuration, builder.Environment);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddLegacyAuthentication(builder.Configuration);
builder.Services.AddLegacyRateLimiting(builder.Configuration);
builder.Services.AddLegacyHealthChecks(builder.Configuration);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // A API só é alcançável pela rede interna do Docker, atrás do nginx (porta não publicada);
    // por isso confiamos no proxy da rede para obter o IP real usado no rate limit.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiResponseHeadersMiddleware>();
app.UseLegacyRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseMiddleware<UserLogContextMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    // OpenAPI só em desenvolvimento (OWASP A02).
    app.MapOpenApi().AllowAnonymous();
}

// Health checks anônimos: consumidos pela orquestração/monitoramento; não expostos pelo nginx.
app.MapLegacyHealthChecks();

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapAddressEndpoints();
app.MapClientLogEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>().SeedAsync(app.Lifetime.ApplicationStopping);
}

await app.RunAsync();

/// <summary>Exposto para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
