namespace Legado.Application.Auth;

/// <summary>Usuário com credenciais verificadas, base para emissão do token.</summary>
/// <param name="Id">Id técnico do usuário (claim <c>sub</c>).</param>
/// <param name="UserName">Login (claim <c>preferred_username</c>).</param>
/// <param name="Roles">Perfis do usuário.</param>
/// <param name="SecurityStamp">Carimbo de segurança atual; muda no logout e invalida os tokens anteriores.</param>
public sealed record AuthenticatedUser(Guid Id, string UserName, IReadOnlyList<string> Roles, string SecurityStamp);
