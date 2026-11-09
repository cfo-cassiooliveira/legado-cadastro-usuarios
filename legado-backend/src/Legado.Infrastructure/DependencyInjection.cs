using Legado.Application.Addresses;
using Legado.Application.Auth;
using Legado.Application.Users;
using Legado.Infrastructure.Addresses;
using Legado.Infrastructure.Auth;
using Legado.Infrastructure.Persistence;
using Legado.Infrastructure.Seeding;
using Legado.Infrastructure.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Legado.Infrastructure;

/// <summary>Registro das implementações de infraestrutura (banco, Identity, serviços de endereço).</summary>
public static class DependencyInjection
{
    /// <summary>Nome da connection string do banco legado em <c>ConnectionStrings</c>.</summary>
    public const string ConnectionStringName = "LegadoDb";

    private const int MinPasswordLength = 8;
    private const int MaxFailedAccessAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan HttpAttemptTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HttpTotalTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Registra banco (EF Core), Identity, serviços de endereço com resiliência e o seed de demonstração.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(TimeProvider.System);
        services.AddPersistence(configuration);
        services.AddIdentityStore();
        services.AddAddressServices(configuration);

        services.AddOptions<SeedOptions>().Bind(configuration.GetSection(SeedOptions.SectionName));
        services.AddScoped<DevelopmentDataSeeder>();
        return services;
    }

    private static void AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddSingleton<AuditInterceptor>();
        services.AddDbContext<LegadoDbContext>((sp, options) => options
            .UseSqlServer(connectionString, sql => sql
                .MigrationsAssembly(LegadoDbContext.MigrationsAssembly)
                .EnableRetryOnFailure())
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));
    }

    private static void AddIdentityStore(this IServiceCollection services)
    {
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = MinPasswordLength;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = MaxFailedAccessAttempts;
                options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<LegadoDbContext>();

        services.AddScoped<IUserAccountWriter, UserAccountWriter>();
        services.AddScoped<IUserReadService, UserReadService>();
        services.AddScoped<ICredentialVerifier, CredentialVerifier>();
        services.AddScoped<IUserSessionStore, UserSessionStore>();
    }

    private static void AddAddressServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AddressServicesOptions>()
            .Bind(configuration.GetSection(AddressServicesOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddMemoryCache();

        services.AddHttpClient<IPostalCodeLookup, ViaCepPostalCodeLookup>((sp, client) =>
                client.BaseAddress = sp.GetRequiredService<IOptions<AddressServicesOptions>>().Value.ViaCepBaseUrl)
            .AddStandardResilienceHandler(ConfigureResilience);

        services.AddHttpClient<IbgeCityDirectory>((sp, client) =>
                client.BaseAddress = sp.GetRequiredService<IOptions<AddressServicesOptions>>().Value.IbgeBaseUrl)
            .AddStandardResilienceHandler(ConfigureResilience);

        services.AddScoped<ICityDirectory>(sp => new CachedCityDirectory(
            sp.GetRequiredService<IbgeCityDirectory>(),
            sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
            sp.GetRequiredService<IOptions<AddressServicesOptions>>()));
    }

    // Timeouts curtos: o cadastro depende desses serviços e não deve travar a requisição (Art. VII.1).
    private static void ConfigureResilience(Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = HttpAttemptTimeout;
        options.TotalRequestTimeout.Timeout = HttpTotalTimeout;
    }
}
