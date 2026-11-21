using Plastoids.Modules.Identity.Features.Auth.IssueToken;
using Plastoids.Modules.Identity.Features.Auth.RegisterWithEmail;
using Plastoids.Modules.Identity.Features.Auth.RequestOtp;
using Plastoids.Modules.Identity.Features.Auth.VerifyOtp;
using Plastoids.Modules.Identity.Features.Me.DeleteMyAccount;
using Plastoids.Modules.Identity.Features.Organisations.RegisterOrganisation;
using Plastoids.Modules.Identity.Features.Organisations.VerifyOrganisation;

namespace Plastoids.Modules.Identity.Tests;

public class ValidatorTests
{
    [Fact]
    public void RequestOtp_requires_a_valid_phone()
    {
        new RequestOtpValidator().Validate(new RequestOtpCommand("12")).IsValid.Should().BeFalse();
        new RequestOtpValidator().Validate(new RequestOtpCommand("08031234567")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyOtp_requires_six_digit_code()
    {
        var v = new VerifyOtpValidator();
        v.Validate(new VerifyOtpCommand("08031234567", "12345", null, null)).IsValid.Should().BeFalse();
        v.Validate(new VerifyOtpCommand("08031234567", "12a456", null, null)).IsValid.Should().BeFalse();
        v.Validate(new VerifyOtpCommand("08031234567", "123456", null, null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void RegisterWithEmail_requires_strong_enough_password()
    {
        var v = new RegisterWithEmailValidator();
        v.Validate(new RegisterWithEmailCommand("a@b.com", "short1")).IsValid.Should().BeFalse();
        v.Validate(new RegisterWithEmailCommand("a@b.com", "onlyletterslong")).IsValid.Should().BeFalse();
        v.Validate(new RegisterWithEmailCommand("not-an-email", "Letters-and-1234")).IsValid.Should().BeFalse();
        v.Validate(new RegisterWithEmailCommand("a@b.com", "Letters-and-1234")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void IssueToken_rules_depend_on_grant_type()
    {
        var v = new IssueTokenValidator();
        v.Validate(new IssueTokenCommand("password", null, null, null, null)).Errors.Select(e => e.PropertyName).Should().Contain(["Email", "Password"]);
        v.Validate(new IssueTokenCommand("refresh_token", null, null, null, null)).Errors.Select(e => e.PropertyName).Should().Equal("RefreshToken");
        v.Validate(new IssueTokenCommand("google", null, null, null, null)).Errors.Select(e => e.PropertyName).Should().Equal("IdToken");
        v.Validate(new IssueTokenCommand("client_credentials", null, null, null, null)).IsValid.Should().BeFalse();
        v.Validate(new IssueTokenCommand("refresh_token", null, null, "abc", null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void DeleteMyAccount_requires_literal_confirmation()
    {
        new DeleteMyAccountValidator().Validate(new DeleteMyAccountCommand("delete")).IsValid.Should().BeFalse();
        new DeleteMyAccountValidator().Validate(new DeleteMyAccountCommand("DELETE")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void RegisterOrganisation_validates_type_and_cac_number()
    {
        var v = new RegisterOrganisationValidator();
        v.Validate(new RegisterOrganisationCommand("Org", "charity", "RC123456", "S", "s@o.com")).IsValid.Should().BeFalse();
        v.Validate(new RegisterOrganisationCommand("Org", "sponsor", "ABC", "S", "s@o.com")).IsValid.Should().BeFalse();
        v.Validate(new RegisterOrganisationCommand("Org", "Sponsor", "RC123456", "S", "s@o.com")).IsValid.Should().BeTrue();
        v.Validate(new RegisterOrganisationCommand("Org", "hub", "1234567", "S", "s@o.com")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyOrganisation_requires_reason_when_rejecting_and_an_idempotency_key()
    {
        var v = new VerifyOrganisationValidator();
        var id = Guid.CreateVersion7();
        v.Validate(new VerifyOrganisationCommand(id, "rejected", null, "k")).IsValid.Should().BeFalse();
        v.Validate(new VerifyOrganisationCommand(id, "verified", null, "")).IsValid.Should().BeFalse();
        v.Validate(new VerifyOrganisationCommand(id, "maybe", null, "k")).IsValid.Should().BeFalse();
        v.Validate(new VerifyOrganisationCommand(id, "verified", null, "k")).IsValid.Should().BeTrue();
    }
}
