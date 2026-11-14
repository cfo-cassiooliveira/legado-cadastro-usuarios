using Legado.Application.Addresses;
using Legado.Application.Auth;
using Legado.Application.Users;
using Legado.Application.Users.Migration;
using Microsoft.Extensions.DependencyInjection;

namespace Legado.Application;

/// <summary>Registro dos casos de uso da camada Application.</summary>
public static class DependencyInjection
{
    /// <summary>Registra os handlers dos casos de uso no contêiner de DI.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IRegisterUserHandler, RegisterUserHandler>();
        services.AddScoped<IListUsersHandler, ListUsersHandler>();
        services.AddScoped<ILoginHandler, LoginHandler>();
        services.AddScoped<ILogoutHandler, LogoutHandler>();
        services.AddScoped<ISessionValidator, SessionValidator>();
        services.AddScoped<ILookupPostalCodeHandler, LookupPostalCodeHandler>();
        services.AddScoped<IListCitiesHandler, ListCitiesHandler>();
        services.AddScoped<IVerifyLegacyCredentialsHandler, VerifyLegacyCredentialsHandler>();
        services.AddScoped<IGetLegacyUserByLoginHandler, GetLegacyUserByLoginHandler>();
        services.AddScoped<IGetLegacyUserExistenceHandler, GetLegacyUserExistenceHandler>();
        services.AddScoped<IMarkUserMigratedHandler, MarkUserMigratedHandler>();
        return services;
    }
}
