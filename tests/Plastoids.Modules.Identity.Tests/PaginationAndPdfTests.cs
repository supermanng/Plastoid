using Plastoids.Infrastructure.Http;

namespace Plastoids.Modules.Identity.Tests;

public class PaginationAndPdfTests
{
    [Fact]
    public void Cursor_round_trips_and_rejects_garbage()
    {
        var id = Guid.CreateVersion7();
        var cursor = Cursor.Encode(id);
        cursor.Should().NotContain("=").And.NotContain("+").And.NotContain("/");
        Cursor.Decode(cursor).Should().Be(id);
        Cursor.Decode("not-a-cursor!").Should().BeNull();
        Cursor.Decode(null).Should().BeNull();
        Cursor.ClampLimit(null).Should().Be(50);
        Cursor.ClampLimit(10_000).Should().Be(200);
        Cursor.ClampLimit(-3).Should().Be(1);
    }
}
