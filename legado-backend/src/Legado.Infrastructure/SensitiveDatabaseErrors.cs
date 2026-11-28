using Microsoft.Data.SqlClient;

namespace Legado.Infrastructure;

/// <summary>
/// Reconhece falhas do SQL Server cuja mensagem repete o valor da linha (chave duplicada e texto truncado), para que a
/// exceção não vá ao log: nesta base o valor pode ser e-mail ou login (Art. IV.6).
/// </summary>
public static class SensitiveDatabaseErrors
{
    // Chave duplicada em índice único (2601) e em constraint UNIQUE/PK (2627); texto truncado com o valor (2628).
    private static readonly int[] NumbersCarryingRowValues = [2601, 2627, 2628];

    /// <summary>Indica se a exceção, ou alguma causa dela, é uma dessas falhas e devolve o número do erro.</summary>
    public static bool CarriesRowValue(Exception exception, out int sqlErrorNumber)
    {
        ArgumentNullException.ThrowIfNull(exception);
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && NumbersCarryingRowValues.Contains(sql.Number))
            {
                sqlErrorNumber = sql.Number;
                return true;
            }
        }

        sqlErrorNumber = 0;
        return false;
    }
}
