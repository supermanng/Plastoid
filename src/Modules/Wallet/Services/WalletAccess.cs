using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Wallet.Contracts;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Wallet.Services;

/// <summary>Who may read or spend which wallet: a person their own; an organisation's any member (reads) or an owner / finance member (purchases).</summary>
internal sealed class WalletAccess(ICurrentUser currentUser, IOrganisationDirectory organisations)
{
    public static readonly IReadOnlyCollection<string> SpendRoles = ["owner", "finance"];

    public async Task<Result<(string OwnerType, Guid OwnerId)>> ResolveAsync(Guid? organisationId, bool spend, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        if (organisationId is not { } orgId)
        {
            return (WalletOwnerTypes.User, userId);
        }

        var allowed = await organisations.IsMemberAsync(userId, orgId, spend ? SpendRoles : null, cancellationToken);
        if (!allowed)
        {
            return Error.Forbidden("organisation_access_denied", spend ? "Only an owner or finance member can buy or spend the organisation's units." : "You are not a member of this organisation.");
        }

        return (WalletOwnerTypes.Organisation, orgId);
    }
}
