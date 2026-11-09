namespace Legado.Domain.Common;

/// <summary>Resultado de uma operação que pode falhar de forma esperada (Result pattern).</summary>
public class Result
{
    /// <summary>Construtor base; use os factories <see cref="Success()"/> e <see cref="Failure(DomainError)"/>.</summary>
    protected Result(IReadOnlyList<DomainError> errors) => Errors = errors;

    /// <summary>Erros da operação; vazio em caso de sucesso.</summary>
    public IReadOnlyList<DomainError> Errors { get; }

    /// <summary>Indica se a operação terminou sem erros.</summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary>Cria um resultado de sucesso sem valor.</summary>
    public static Result Success() => new([]);

    /// <summary>Cria uma falha com um ou mais erros.</summary>
    public static Result Failure(IReadOnlyList<DomainError> errors) => new(EnsureNotEmpty(errors));

    /// <summary>Cria uma falha com um único erro.</summary>
    public static Result Failure(DomainError error) => Failure([error]);

    /// <summary>Cria um resultado de sucesso com valor.</summary>
    public static Result<T> Success<T>(T value) => new(value, []);

    /// <summary>Cria uma falha tipada com um ou mais erros.</summary>
    public static Result<T> Failure<T>(IReadOnlyList<DomainError> errors) => new(default, EnsureNotEmpty(errors));

    /// <summary>Cria uma falha tipada com um único erro.</summary>
    public static Result<T> Failure<T>(DomainError error) => Failure<T>([error]);

    private static IReadOnlyList<DomainError> EnsureNotEmpty(IReadOnlyList<DomainError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        // Falha sem erro tornaria o resultado ambíguo; falhamos rápido.
        return errors.Count == 0
            ? throw new ArgumentException("A failure must contain at least one error.", nameof(errors))
            : errors;
    }
}

/// <summary>Resultado com valor em caso de sucesso. Crie via <see cref="Result.Success{T}(T)"/> e <see cref="Result.Failure{T}(DomainError)"/>.</summary>
/// <typeparam name="T">Tipo do valor retornado no sucesso.</typeparam>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    /// <summary>Construtor usado pelos factories de <see cref="Result"/>.</summary>
    internal Result(T? value, IReadOnlyList<DomainError> errors) : base(errors) => _value = value;

    /// <summary>Valor do sucesso; acessar em falha é erro de programação.</summary>
    public T Value => IsSuccess
        ? _value!  // Em sucesso o valor sempre foi atribuído pelo factory Success.
        : throw new InvalidOperationException("Cannot access the value of a failed result.");
}
