using Plastoids.Modules.Certificates.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Learning.Tests;

public class CertificateTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Certificate_has_a_readable_code_and_a_verifiable_hash()
    {
        var c = Certificate.Issue(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), " Ada Okafor ", Guid.NewGuid(), "SQL for Analysts", Guid.NewGuid(), 1, 87.5m, "course:x", Now);
        c.PublicCode.Should().MatchRegex("^[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}$");
        c.Hash.Should().HaveLength(64);
        c.HashMatches.Should().BeTrue();
        c.LearnerName.Should().Be("Ada Okafor");
        Certificate.ComputeHash(c.PublicCode, c.EnrolmentId, c.LearnerId, c.CourseId, "Tampered title", c.LearnerName, c.IssuedAt, c.FinalScore).Should().NotBe(c.Hash);
    }

    [Fact]
    public void Revocation_keeps_the_record_and_happens_once()
    {
        var c = Certificate.Issue(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), "Ada", Guid.NewGuid(), "Course", Guid.NewGuid(), 1, 70m, "course:x", Now);
        c.IsValid.Should().BeTrue();
        c.Revoke(Guid.NewGuid(), "Plagiarism confirmed", Now.AddDays(1)).IsSuccess.Should().BeTrue();
        c.IsValid.Should().BeFalse();
        c.RevokedAt.Should().Be(Now.AddDays(1));
        c.Revoke(Guid.NewGuid(), "again", Now.AddDays(2)).Error.Code.Should().Be("certificate_already_revoked");
        c.HashMatches.Should().BeTrue("revocation does not alter the issued facts");
    }

    [Fact]
    public void Public_codes_are_unique_enough()
    {
        var codes = Enumerable.Range(0, 2000).Select(_ => Certificate.NewPublicCode()).ToHashSet();
        codes.Should().HaveCount(2000);
    }
}
