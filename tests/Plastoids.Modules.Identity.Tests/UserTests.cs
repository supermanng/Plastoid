using Plastoids.Modules.Identity.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Identity.Tests;

public class UserTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private static User Adult() => User.Register(Tenants.Marketplace, "enc-phone", "hash", "enc-dob", new DateOnly(2000, 1, 1), "  Ada  ", Now).Value;

    [Fact]
    public void Register_creates_an_active_user_with_trimmed_name()
    {
        var user = Adult();
        user.Status.Should().Be(UserStatus.Active);
        user.DisplayName.Should().Be("Ada");
        user.Id.Should().NotBeEmpty();
        user.TenantId.Should().Be(Tenants.Marketplace);
        user.MfaEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData(2008, 11, 22)] // turns 18 tomorrow
    [InlineData(2015, 1, 1)]
    public void Register_blocks_under_18(int y, int m, int d)
    {
        var result = User.Register(Tenants.Marketplace, "p", "h", "d", new DateOnly(y, m, d), "Kid", Now);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("under_18");
    }

    [Fact]
    public void Register_allows_exactly_18_today()
    {
        var result = User.Register(Tenants.Marketplace, "p", "h", "d", new DateOnly(2008, 11, 21), "Adult", Now);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Register_rejects_future_dob()
    {
        var result = User.Register(Tenants.Marketplace, "p", "h", "d", new DateOnly(2030, 1, 1), "X", Now);
        result.Error.Code.Should().Be("dob_in_future");
    }

    [Fact]
    public void Mfa_must_be_enrolled_before_confirmed_and_cannot_be_re_enrolled_once_enabled()
    {
        var user = Adult();
        user.ConfirmMfa().Error.Code.Should().Be("mfa_not_enrolled");
        user.BeginMfaEnrolment("secret").IsSuccess.Should().BeTrue();
        user.ConfirmMfa().IsSuccess.Should().BeTrue();
        user.MfaEnabled.Should().BeTrue();
        user.BeginMfaEnrolment("other").Error.Code.Should().Be("mfa_already_enabled");
    }

    [Fact]
    public void Email_is_normalised_and_verification_reset_on_change()
    {
        var user = Adult();
        user.SetEmailAndPassword("  Ada@Example.COM ", "hash").IsSuccess.Should().BeTrue();
        user.Email.Should().Be("ada@example.com");
        user.UpdateProfile(null, "new@example.com").IsSuccess.Should().BeTrue();
        user.Email.Should().Be("new@example.com");
        user.EmailVerifiedAt.Should().BeNull();
    }

    [Fact]
    public void Goals_are_trimmed_deduplicated_and_capped_at_20()
    {
        var user = Adult();
        user.UpdateGoals("  Data analyst ", Enumerable.Range(0, 30).Select(i => $"topic{i % 25}").Concat(["  ", "Topic0"]));
        user.CareerGoal.Should().Be("Data analyst");
        user.Interests.Should().HaveCount(20).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void Anonymise_wipes_personal_columns_but_keeps_the_id()
    {
        var user = Adult();
        user.SetEmailAndPassword("a@b.c", "hash");
        user.BeginMfaEnrolment("s");
        user.ConfirmMfa();
        var id = user.Id;

        user.Anonymise(Now);

        user.Id.Should().Be(id);
        user.Status.Should().Be(UserStatus.Deleted);
        user.DeletedAt.Should().Be(Now);
        user.Email.Should().BeNull();
        user.PasswordHash.Should().BeNull();
        user.PhoneCiphertext.Should().BeEmpty();
        user.DobCiphertext.Should().BeEmpty();
        user.PhoneHash.Should().StartWith("deleted:");
        user.MfaEnabled.Should().BeFalse();
        user.MfaSecretCiphertext.Should().BeNull();
        user.DisplayName.Should().Be("Deleted user");
        user.IsActive.Should().BeFalse();
        user.SetEmailAndPassword("x@y.z", "h").Error.Code.Should().Be("account_inactive");
    }
}
