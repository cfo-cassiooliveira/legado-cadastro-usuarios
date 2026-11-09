using Legado.Domain.Common;

namespace Legado.Domain.Tests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Value_OnFailure_Throws()
    {
        var result = Result.Failure<int>(new DomainError("code", "field", "description"));

        Should.Throw<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void Failure_WithoutErrors_Throws()
    {
        Should.Throw<ArgumentException>(() => Result.Failure<int>([]));
    }

    [Fact]
    public void Success_HasNoErrors()
    {
        var result = Result.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.Errors.ShouldBeEmpty();
    }
}
