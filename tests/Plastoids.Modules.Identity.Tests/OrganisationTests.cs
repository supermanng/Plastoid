using Plastoids.Modules.Identity.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Identity.Tests;

public class OrganisationTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Staff = Guid.CreateVersion7();

    [Fact]
    public void Register_normalises_fields_and_starts_pending()
    {
        var org = Organisation.Register(Tenants.Marketplace, " Zenith CSR ", OrganisationType.Sponsor, " rc123456 ", " Mrs Adebayo ", " CSR@Zenith.com ");
        org.Name.Should().Be("Zenith CSR");
        org.CacNumber.Should().Be("RC123456");
        org.SignatoryEmail.Should().Be("csr@zenith.com");
        org.VerificationStatus.Should().Be(VerificationStatus.Pending);
        org.IsVerified.Should().BeFalse();
    }

    [Fact]
    public void Verify_then_reject_is_refused()
    {
        var org = Organisation.Register(Tenants.Marketplace, "Org", OrganisationType.Sponsor, "RC1", "S", "s@o.com");
        org.Verify(Staff, Now).IsSuccess.Should().BeTrue();
        org.VerifiedBy.Should().Be(Staff);
        org.VerifiedAt.Should().Be(Now);
        org.Verify(Staff, Now).Error.Code.Should().Be("organisation_already_verified");
        org.Reject(Staff, "late", Now).Error.Code.Should().Be("organisation_already_verified");
    }

    [Fact]
    public void Reject_records_reason_and_can_later_be_verified()
    {
        var org = Organisation.Register(Tenants.Marketplace, "Org", OrganisationType.Institution, "RC2", "S", "s@o.com");
        org.Reject(Staff, " CAC mismatch ", Now).IsSuccess.Should().BeTrue();
        org.VerificationStatus.Should().Be(VerificationStatus.Rejected);
        org.RejectionReason.Should().Be("CAC mismatch");
        org.Verify(Staff, Now).IsSuccess.Should().BeTrue();
        org.RejectionReason.Should().BeNull();
    }

    [Fact]
    public void Organisation_roles_are_the_blueprint_four_plus_author()
    {
        // ADR 0019 adds "author": may create and edit the organisation's courses and engage tutors.
        OrganisationRoles.All.Should().BeEquivalentTo(["owner", "finance", "reviewer", "viewer", "author"]);
        OrganisationRoles.IsValid("admin").Should().BeFalse();
    }
}
