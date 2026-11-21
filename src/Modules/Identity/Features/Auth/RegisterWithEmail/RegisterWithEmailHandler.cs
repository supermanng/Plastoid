using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Identity.Features.Auth.RegisterWithEmail;

internal sealed class RegisterWithEmailHandler(IdentityDbContext db, IPasswordHasher hasher, ICurrentUser currentUser) : ICommandHandler<RegisterWithEmailCommand>
{
    public async Task<Result> Handle(RegisterWithEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken);
        if (user is null)
        {
            return Error.Unauthorized("not_signed_in", "Sign in with your phone first.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != user.Id, cancellationToken))
        {
            return Error.Conflict("email_in_use", "That email is already linked to another account.");
        }

        return user.SetEmailAndPassword(email, hasher.Hash(request.Password));
    }
}
