namespace Legado.Domain.Common;

/// <summary>Categoria do erro, usada pela borda HTTP para escolher o status code.</summary>
public enum ErrorType
{
    /// <summary>Dados de entrada inválidos (HTTP 400).</summary>
    Validation,
    /// <summary>Recurso inexistente (HTTP 404).</summary>
    NotFound,
    /// <summary>Conflito com o estado atual, ex.: duplicidade (HTTP 409).</summary>
    Conflict,
    /// <summary>Falha de autenticação (HTTP 401).</summary>
    Unauthorized,
}
