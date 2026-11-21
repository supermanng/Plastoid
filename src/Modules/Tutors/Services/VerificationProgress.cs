using Plastoids.Modules.Tutors.Contracts;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Tutors.Services;

/// <summary>After any stage completes: flip to Verified when all three passed and publish tutors.tutor-verified.v1 once.</summary>
internal static class VerificationProgress
{
    public static void Advance(TutorProfile profile, IOutbox outbox, DateTime utcNow)
    {
        if (profile.TryVerify(utcNow))
        {
            outbox.Enqueue(new TutorVerified(profile.Id, profile.Id, profile.UserId, profile.Level.ToString().ToLowerInvariant()));
        }
    }
}
