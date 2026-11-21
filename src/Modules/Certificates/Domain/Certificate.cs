using System.Security.Cryptography;
using System.Text;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Certificates.Domain;

/// <summary>
/// Blueprint §9.14: immutable once issued (the database trigger rejects changes to the identifying columns);
/// revocation keeps the record and the verification page shows "revoked". The hash lets a printed certificate be checked offline.
/// </summary>
[Audited]
public sealed class Certificate : Entity
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I ambiguity on printed copies

    private Certificate()
    {
    }

    private Certificate(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid EnrolmentId { get; private set; }
    public Guid LearnerId { get; private set; }
    public string LearnerName { get; private set; } = string.Empty;
    public Guid CourseId { get; private set; }
    public string CourseTitle { get; private set; } = string.Empty;
    public Guid OfferingId { get; private set; }
    public int VersionNo { get; private set; }
    public decimal FinalScore { get; private set; }
    public string PublicCode { get; private set; } = string.Empty;
    public string Hash { get; private set; } = string.Empty;
    public string TemplateKey { get; private set; } = string.Empty;
    public DateTime IssuedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }
    public Guid? RevokedBy { get; private set; }

    public static Certificate Issue(Guid tenantId, Guid enrolmentId, Guid learnerId, string learnerName, Guid courseId, string courseTitle, Guid offeringId, int versionNo, decimal finalScore, string templateKey, DateTime issuedAt)
    {
        var code = NewPublicCode();
        var certificate = new Certificate(NewId(), tenantId)
        {
            EnrolmentId = enrolmentId,
            LearnerId = learnerId,
            LearnerName = learnerName.Trim(),
            CourseId = courseId,
            CourseTitle = courseTitle.Trim(),
            OfferingId = offeringId,
            VersionNo = versionNo,
            FinalScore = Math.Round(finalScore, 2),
            PublicCode = code,
            TemplateKey = templateKey,
            IssuedAt = issuedAt,
        };
        certificate.Hash = ComputeHash(code, enrolmentId, learnerId, courseId, certificate.CourseTitle, certificate.LearnerName, issuedAt, certificate.FinalScore);
        return certificate;
    }

    public static string NewPublicCode()
    {
        Span<char> chars = stackalloc char[12];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        }

        return $"{new string(chars[..4])}-{new string(chars[4..8])}-{new string(chars[8..])}";
    }

    /// <summary>Canonical hash: SHA-256 over the fields a verifier can read off the certificate.</summary>
    public static string ComputeHash(string publicCode, Guid enrolmentId, Guid learnerId, Guid courseId, string courseTitle, string learnerName, DateTime issuedAt, decimal finalScore)
    {
        var canonical = string.Join('|', publicCode, enrolmentId.ToString("N"), learnerId.ToString("N"), courseId.ToString("N"), courseTitle, learnerName, CanonicalIssuedAt(issuedAt), finalScore.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    /// <summary>Whole seconds in UTC: MySQL stores microseconds and reads back Kind=Unspecified, so the printed instant must survive the round trip.</summary>
    private static string CanonicalIssuedAt(DateTime issuedAt)
        => new DateTime(issuedAt.Ticks - (issuedAt.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    public bool IsValid => RevokedAt is null;

    public bool HashMatches => Hash == ComputeHash(PublicCode, EnrolmentId, LearnerId, CourseId, CourseTitle, LearnerName, IssuedAt, FinalScore);

    public Result Revoke(Guid staffId, string reason, DateTime utcNow)
    {
        if (RevokedAt is not null)
        {
            return Error.Conflict("certificate_already_revoked", "This certificate is already revoked.");
        }

        RevokedAt = utcNow;
        RevokedBy = staffId;
        RevocationReason = reason.Trim();
        return Result.Success();
    }
}

/// <summary>Every verification page hit, for fraud signals (blueprint §19: audit of certificate verification).</summary>
public sealed class VerificationLog : Entity
{
    private VerificationLog()
    {
    }

    private VerificationLog(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public string PublicCode { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = string.Empty;
    public string? Ip { get; private set; }
    public DateTime VerifiedAt { get; private set; }

    public static VerificationLog Record(Guid tenantId, string publicCode, string outcome, string? ip, DateTime utcNow)
        => new(NewId(), tenantId) { PublicCode = publicCode, Outcome = outcome, Ip = ip, VerifiedAt = utcNow };
}
