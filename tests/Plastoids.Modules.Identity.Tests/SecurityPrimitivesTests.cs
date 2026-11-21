using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Identity.Domain;

namespace Plastoids.Modules.Identity.Tests;

public class SecurityPrimitivesTests
{
    [Theory]
    [InlineData("0803 123 4567", "+2348031234567")]
    [InlineData("08031234567", "+2348031234567")]
    [InlineData("+234 803 123 4567", "+2348031234567")]
    [InlineData("2348031234567", "+2348031234567")]
    [InlineData("+447911123456", "+447911123456")]
    public void Phone_numbers_normalise_to_e164(string input, string expected)
    {
        PhoneNumber.TryNormalise(input, out var e164).Should().BeTrue();
        e164.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("803123456")]
    [InlineData("+0123456789")]
    public void Invalid_phone_numbers_are_rejected(string input)
    {
        PhoneNumber.TryNormalise(input, out _).Should().BeFalse();
    }

    [Fact]
    public void Phone_mask_hides_the_middle()
    {
        PhoneNumber.Mask("+2348031234567").Should().Be("+2348031****67");
    }

    [Fact]
    public void Totp_matches_rfc6238_vector_and_accepts_one_step_of_drift()
    {
        // RFC 6238 Appendix B, SHA-1, secret "12345678901234567890", T=59 → 94287082 (8 digits) → last 6 = 287082.
        var secret = System.Text.Encoding.ASCII.GetBytes("12345678901234567890");
        var t = DateTime.UnixEpoch.AddSeconds(59);
        Totp.Compute(secret, t).Should().Be("287082");
        Totp.Verify(secret, "287082", t).Should().BeTrue();
        Totp.Verify(secret, "287082", t.AddSeconds(30)).Should().BeTrue("previous step is accepted");
        Totp.Verify(secret, "287082", t.AddSeconds(90)).Should().BeFalse();
        Totp.Verify(secret, "000000", t).Should().BeFalse();
    }

    [Fact]
    public void Base32_round_trips_and_matches_known_encoding()
    {
        Base32.Encode("Hello!"u8.ToArray()).Should().Be("JBSWY3DPEE");
        var bytes = Totp.GenerateSecret();
        Base32.Decode(Base32.Encode(bytes)).Should().Equal(bytes);
    }

    [Fact]
    public void Field_encryptor_round_trips_and_blind_index_is_deterministic_and_case_insensitive()
    {
        var encryptor = BuildEncryptor();
        var cipher1 = encryptor.Encrypt("+2348031234567");
        var cipher2 = encryptor.Encrypt("+2348031234567");
        cipher1.Should().NotBe(cipher2, "nonces are random");
        encryptor.Decrypt(cipher1).Should().Be("+2348031234567");
        encryptor.BlindIndex("abc@x.com").Should().Be(encryptor.BlindIndex(" ABC@X.COM "));
        encryptor.BlindIndex("a").Should().NotBe(encryptor.BlindIndex("b"));
    }

    [Fact]
    public void Password_hasher_verifies_and_rejects()
    {
        var hasher = BuildServices().GetRequiredService<IPasswordHasher>();
        var hash = hasher.Hash("Correct-Horse-9");
        hasher.Verify(hash, "Correct-Horse-9").Should().Be(PasswordVerification.Success);
        hasher.Verify(hash, "wrong").Should().Be(PasswordVerification.Failed);
    }

    [Fact]
    public void Token_service_issues_access_tokens_and_rejects_mfa_challenge_reuse()
    {
        var tokens = BuildServices().GetRequiredService<ITokenService>();
        var userId = Guid.CreateVersion7();
        var access = tokens.IssueAccessToken(new TokenSubject(userId, Guid.CreateVersion7(), ["learner"], [], ["otp"]));
        access.Token.Split('.').Should().HaveCount(3);
        tokens.ValidateMfaChallenge(access.Token).Should().BeNull("an access token is not an MFA challenge");

        var challenge = tokens.IssueMfaChallenge(userId, Guid.CreateVersion7());
        tokens.ValidateMfaChallenge(challenge.Token).Should().Be(userId);
        tokens.ValidateMfaChallenge(challenge.Token + "x").Should().BeNull();
    }

    private static IFieldEncryptor BuildEncryptor() => BuildServices().GetRequiredService<IFieldEncryptor>();

    internal static ServiceProvider BuildServices()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Secrets:FieldEncryptionKey"] = Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray()),
            ["Secrets:BlindIndexKey"] = Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)(31 - i)).ToArray()),
            ["Secrets:JwtSigningKey"] = "unit-test-signing-key-0123456789abcdef0123456789",
            ["ConnectionStrings:Plastoids"] = "Server=localhost;Database=x;User=x;Password=x",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddPlastoidsInfrastructure(configuration);
        services.AddPlastoidsAuthentication(configuration);
        return services.BuildServiceProvider();
    }
}
