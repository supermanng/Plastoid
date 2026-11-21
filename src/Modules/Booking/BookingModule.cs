using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Infrastructure.Messaging.Kafka;
using Plastoids.Infrastructure.Modules;
using Plastoids.Modules.Booking.Consumers;
using Plastoids.Modules.Booking.Contracts;
using Plastoids.Modules.Booking.Features.Sessions.CancelBooking;
using Plastoids.Modules.Booking.Features.Sessions.CreateBooking;
using Plastoids.Modules.Booking.Features.Sessions.CreateSessionPackage;
using Plastoids.Modules.Booking.Features.Sessions.GetBooking;
using Plastoids.Modules.Booking.Features.Sessions.ListMyBookings;
using Plastoids.Modules.Booking.Features.Sessions.LockSlot;
using Plastoids.Modules.Booking.Features.Sessions.QuoteBooking;
using Plastoids.Modules.Booking.Features.Sessions.RescheduleBooking;
using Plastoids.Modules.Booking.Jobs;
using Plastoids.Modules.Booking.Persistence;
using Plastoids.Modules.Booking.Services;
using Plastoids.Modules.Payments.Contracts;
using Quartz;

namespace Plastoids.Modules.Booking;

public sealed class BookingModule : IModule
{
    public string Name => "booking";
    public Assembly Assembly => typeof(BookingModule).Assembly;
    public Type DbContextType => typeof(BookingDbContext);

    public IEnumerable<TopicDefinition> ProducedTopics =>
    [
        new(EnvelopeSerializer.GetAttribute(typeof(BookingConfirmed)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(BookingCancelled)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(BookingRescheduled)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(EarningReleased)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(TutorStrikeRecorded)).Topic),
        new(EnvelopeSerializer.GetAttribute(typeof(NotificationRequested)).Topic),
    ];

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<SlotLocks>();
        services.AddScoped<BookingPricing>();
        services.AddScoped<BookingLifecycle>();
        services.AddScoped<ReminderDispatcher>();
        services.AddScoped<BookingLifecycleJob>();
        services.AddQuartz(q =>
        {
            var lifecycle = new JobKey(BookingLifecycleJob.JobName);
            q.AddJob<BookingLifecycleJob>(j => j.WithIdentity(lifecycle).StoreDurably());
            q.AddTrigger(t => t.ForJob(lifecycle).WithIdentity(lifecycle.Name + "-trigger").StartNow().WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMinutes(5)).RepeatForever()));
            var reminders = new JobKey(ReminderJob.JobName);
            q.AddJob<ReminderJob>(j => j.WithIdentity(reminders).StoreDurably());
            q.AddTrigger(t => t.ForJob(reminders).WithIdentity(reminders.Name + "-trigger").StartNow().WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMinutes(5)).RepeatForever()));
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var bookings = endpoints.MapGroup("/bookings").WithTags("Bookings");
        QuoteBookingEndpoint.Map(bookings);
        LockSlotEndpoint.Map(bookings);
        CreateBookingEndpoint.Map(bookings);
        CreateSessionPackageEndpoint.Map(bookings);
        ListMyBookingsEndpoint.Map(bookings);
        GetBookingEndpoint.Map(bookings);
        CancelBookingEndpoint.Map(bookings);
        RescheduleBookingEndpoint.Map(bookings);
    }

    public IEnumerable<ConsumerRegistration> Consumers() =>
    [
        ConsumerRegistry.For<PaymentSucceeded, PaymentSucceededConsumer, BookingDbContext>(Name),
    ];
}
