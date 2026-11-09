namespace Legado.Application.Addresses;

/// <summary>Serviço externo de endereços (CEP/municípios) indisponível após as tentativas de resiliência.</summary>
public sealed class AddressServiceUnavailableException : Exception
{
    /// <summary>Cria a exceção sem mensagem.</summary>
    public AddressServiceUnavailableException()
    {
    }

    /// <summary>Cria a exceção com mensagem técnica (inglês).</summary>
    public AddressServiceUnavailableException(string message) : base(message)
    {
    }

    /// <summary>Cria a exceção preservando a causa original (falha de rede, timeout, circuito aberto).</summary>
    public AddressServiceUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
