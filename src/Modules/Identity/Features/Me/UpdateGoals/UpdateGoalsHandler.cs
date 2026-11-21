using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Identity.Features.Me.UpdateGoals;

internal sealed class UpdateGoalsHandler(IdentityDbContext db, ICurrentUser currentUser) : ICommandHandler<UpdateGoalsCommand>
{
    public async Task<Result> Handle(UpdateGoalsCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        user.UpdateGoals(request.CareerGoal, request.Interests);
        return Result.Success();
    }
}
