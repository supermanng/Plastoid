using Plastoids.Modules.Lms.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Sessions.Tests;

public class LiveSessionTests
{
    private static readonly DateTime Start = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Host = Guid.CreateVersion7();
    private static readonly Guid Learner = Guid.CreateVersion7();

    private static LiveSession Session() => LiveSession.Schedule(Tenants.Marketplace, LiveSessionSources.Booking, Guid.NewGuid(), null, Host, [Learner, Learner], "1:1", Start, Start.AddHours(1), "fake", "room_1", "https://live/room_1");

    [Fact]
    public void Only_host_and_participants_can_join_within_the_window()
    {
        var s = Session();
        s.ParticipantUserIds.Should().HaveCount(1, "duplicates removed");
        s.CanJoin(Host).Should().BeTrue();
        s.CanJoin(Learner).Should().BeTrue();
        s.CanJoin(Guid.NewGuid()).Should().BeFalse();
        s.CheckJoinWindow(Start.AddMinutes(-16)).Error.Code.Should().Be("join_too_early");
        s.CheckJoinWindow(Start.AddMinutes(-15)).IsSuccess.Should().BeTrue();
        s.CheckJoinWindow(Start.AddMinutes(89)).IsSuccess.Should().BeTrue("30-minute grace after the end");
        s.CheckJoinWindow(Start.AddMinutes(91)).Error.Code.Should().Be("session_over");
        s.Cancel();
        s.CheckJoinWindow(Start).Error.Code.Should().Be("session_cancelled");
    }

    [Fact]
    public void Attendance_span_records_duration_once()
    {
        var a = AttendanceEvent.Join(Tenants.Marketplace, Guid.NewGuid(), Learner, Start, "evt_1");
        a.IsOpen.Should().BeTrue();
        a.Leave(Start.AddMinutes(42));
        a.DurationSeconds.Should().Be(42 * 60);
        a.Leave(Start.AddMinutes(50));
        a.DurationSeconds.Should().Be(42 * 60, "first leave wins");
        var clockSkew = AttendanceEvent.Join(Tenants.Marketplace, Guid.NewGuid(), Learner, Start, "evt_2");
        clockSkew.Leave(Start.AddMinutes(-1));
        clockSkew.DurationSeconds.Should().Be(0);
    }

    [Fact]
    public void Session_status_transitions()
    {
        var s = Session();
        s.MarkLive();
        s.Status.Should().Be(LiveSessionStatus.Live);
        s.End(Start.AddMinutes(30));
        s.Status.Should().Be(LiveSessionStatus.Live, "not past the end yet");
        s.End(Start.AddMinutes(61));
        s.Status.Should().Be(LiveSessionStatus.Ended);
        s.Move(Start.AddDays(1), Start.AddDays(1).AddHours(1));
        s.StartsAt.Should().Be(Start.AddDays(1));
    }
}
