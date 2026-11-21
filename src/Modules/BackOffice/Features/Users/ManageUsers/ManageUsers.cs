using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Tutors.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.BackOffice.Features.Users.ManageUsers;

/// <summary>User / tutor / sponsor management (blueprint §9.20) through Identity and Tutors contracts.</summary>
public sealed record SearchUsersQuery(string? Query, string? Role, string? Status, int? Limit) : IQuery<IReadOnlyList<UserAdminSummary>>;

public sealed record GetUserQuery(Guid UserId) : IQuery<UserAdminSummary>;

public sealed record SuspendUserCommand(Guid UserId, string Reason, string IdempotencyKey) : ICommand<UserAdminSummary>, IIdempotentCommand;

public sealed record SuspendUserRequest(string Reason);

public sealed record ReinstateUserCommand(Guid UserId, string IdempotencyKey) : ICommand<UserAdminSummary>, IIdempotentCommand;

public sealed record ListTutorsQuery(string? Status, int? Limit) : IQuery<IReadOnlyList<TutorAdminSummary>>;

public sealed record ListSponsorsQuery(string? VerificationStatus, int? Limit) : IQuery<IReadOnlyList<OrganisationAdminSummary>>;

internal sealed class SuspendUserValidator : AbstractValidator<SuspendUserCommand>
{
    public SuspendUserValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class ReinstateUserValidator : AbstractValidator<ReinstateUserCommand>
{
    public ReinstateUserValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class SearchUsersHandler(IUserAdministration users) : IQueryHandler<SearchUsersQuery, IReadOnlyList<UserAdminSummary>>
{
    public async Task<Result<IReadOnlyList<UserAdminSummary>>> Handle(SearchUsersQuery request, CancellationToken ct)
        => Result.Success(await users.SearchUsersAsync(request.Query, request.Role, request.Status, request.Limit ?? 50, ct));
}

internal sealed class GetUserHandler(IUserAdministration users) : IQueryHandler<GetUserQuery, UserAdminSummary>
{
    public async Task<Result<UserAdminSummary>> Handle(GetUserQuery request, CancellationToken ct)
        => await users.GetUserAsync(request.UserId, ct) is { } user ? user : Error.NotFound("user_not_found", "No such user.");
}

internal sealed class SuspendUserHandler(IUserAdministration users, ICurrentUser currentUser) : ICommandHandler<SuspendUserCommand, UserAdminSummary>
{
    public Task<Result<UserAdminSummary>> Handle(SuspendUserCommand request, CancellationToken ct)
        => users.SuspendAsync(request.UserId, request.Reason, currentUser.UserId ?? Guid.Empty, ct);
}

internal sealed class ReinstateUserHandler(IUserAdministration users, ICurrentUser currentUser) : ICommandHandler<ReinstateUserCommand, UserAdminSummary>
{
    public Task<Result<UserAdminSummary>> Handle(ReinstateUserCommand request, CancellationToken ct)
        => users.ReinstateAsync(request.UserId, currentUser.UserId ?? Guid.Empty, ct);
}

internal sealed class ListTutorsHandler(ITutorVerificationQueue tutors) : IQueryHandler<ListTutorsQuery, IReadOnlyList<TutorAdminSummary>>
{
    public async Task<Result<IReadOnlyList<TutorAdminSummary>>> Handle(ListTutorsQuery request, CancellationToken ct)
        => Result.Success(await tutors.ListTutorsAsync(request.Status, request.Limit ?? 50, ct));
}

internal sealed class ListSponsorsHandler(IUserAdministration users) : IQueryHandler<ListSponsorsQuery, IReadOnlyList<OrganisationAdminSummary>>
{
    public async Task<Result<IReadOnlyList<OrganisationAdminSummary>>> Handle(ListSponsorsQuery request, CancellationToken ct)
        => Result.Success(await users.ListOrganisationsAsync(request.VerificationStatus, request.Limit ?? 50, ct));
}

internal static class ManageUsersEndpoints
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapGet("/users", async (string? query, string? role, string? status, int? limit, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SearchUsersQuery(query, role, status, limit), ct)).ToHttpResult())
            .WithName("BackOfficeSearchUsers")
            .WithSummary("Staff: search accounts by name, email or id; filter by role and status.")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapGet("/users/{userId:guid}", async (Guid userId, ISender sender, CancellationToken ct) => (await sender.Send(new GetUserQuery(userId), ct)).ToHttpResult())
            .WithName("BackOfficeGetUser")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapPost("/users/{userId:guid}/suspend", async (Guid userId, SuspendUserRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new SuspendUserCommand(userId, body.Reason, key), ct)).ToHttpResult();
            })
            .WithName("BackOfficeSuspendUser")
            .WithSummary("Staff: suspend an account (sessions revoked; audited with before/after). Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapPost("/users/{userId:guid}/reinstate", async (Guid userId, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ReinstateUserCommand(userId, key), ct)).ToHttpResult();
            })
            .WithName("BackOfficeReinstateUser")
            .WithSummary("Staff: reinstate a suspended account. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapGet("/tutors", async (string? status, int? limit, ISender sender, CancellationToken ct) => (await sender.Send(new ListTutorsQuery(status, limit), ct)).ToHttpResult())
            .WithName("BackOfficeListTutors")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapGet("/sponsors", async (string? verificationStatus, int? limit, ISender sender, CancellationToken ct) => (await sender.Send(new ListSponsorsQuery(verificationStatus, limit), ct)).ToHttpResult())
            .WithName("BackOfficeListSponsors")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
