using Plastoids.SharedKernel.Results;

namespace Plastoids.SharedKernel.Tests;

public class ResultTests
{
    [Fact]
    public void Success_carries_value_and_no_error()
    {
        var r = Result.Success(42);
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be(42);
        r.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_carries_error_and_value_throws()
    {
        var error = Error.NotFound("course_not_found", "No such course.");
        Result<int> r = error;
        r.IsFailure.Should().BeTrue();
        r.Error.Type.Should().Be(ErrorType.NotFound);
        var act = () => r.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Match_dispatches()
    {
        Result.Success("x").Match(v => v + "!", e => e.Code).Should().Be("x!");
        Result.Failure<string>(Error.Conflict("dup", "dup")).Match(v => v, e => e.Code).Should().Be("dup");
    }

    [Fact]
    public void Inconsistent_construction_is_rejected()
    {
        var act = () => Result.Failure(Error.None);
        act.Should().Throw<ArgumentException>();
    }
}
