using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Legado.Infrastructure.Persistence;

/// <summary>
/// Classifica falhas previstas do SQL Server ao gravar (ex.: violação de índice único em corrida de cadastro),
/// para que sejam tratadas como regra de negócio (Warning + 409) e não como erro inesperado.
/// </summary>
internal static partial class DatabaseErrors
{
    // Números de erro do SQL Server para chave duplicada em índice único e em constraint UNIQUE/PK.
    private const int DuplicateKeyInUniqueIndex = 2601;
    private const int UniqueConstraintViolation = 2627;

    /// <summary>
    /// Indica se a falha foi violação de unicidade e devolve o nome do índice/constraint violado.
    /// A mensagem original não é exposta porque contém o valor duplicado (dado pessoal).
    /// </summary>
    public static bool IsUniqueViolation(DbUpdateException exception, out string indexName, out int sqlErrorNumber)
    {
        ArgumentNullException.ThrowIfNull(exception);
        indexName = string.Empty;
        sqlErrorNumber = 0;

        if (exception.InnerException is not SqlException { Number: DuplicateKeyInUniqueIndex or UniqueConstraintViolation } sql)
        {
            return false;
        }

        sqlErrorNumber = sql.Number;
        indexName = IndexNamePattern().Match(sql.Message) is { Success: true } match ? match.Groups["name"].Value : "unknown";
        return true;
    }

    [GeneratedRegex("(?:unique index|constraint) '(?<name>[^']+)'", RegexOptions.IgnoreCase)]
    private static partial Regex IndexNamePattern();
}
