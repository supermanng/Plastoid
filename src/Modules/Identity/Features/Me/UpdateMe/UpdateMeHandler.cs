using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Identity.Features.Me.UpdateMe;

internal sealed class UpdateMeHandler(IdentityDbContext db, ICurrentUser currentUser) : ICommandHandler<UpdateMeCommand>
{
    public async Task<Result> Handle(UpdateMeCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken);
        if (user is null)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        if (request.Email is not null)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(u => u.Email == email && u.Id != user.Id, cancellationToken))
            {
                return Error.Conflict("email_in_use", "That email is already linked to another account.");
            }
        }

        return user.UpdateProfile(request.DisplayName, request.Email);
    }
}
