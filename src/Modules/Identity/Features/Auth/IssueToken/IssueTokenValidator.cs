using FluentValidation;

namespace Plastoids.Modules.Identity.Features.Auth.IssueToken;

internal sealed class IssueTokenValidator : AbstractValidator<IssueTokenCommand>
{
    public IssueTokenValidator()
    {
        RuleFor(x => x.GrantType).NotEmpty().Must(g => g is GrantTypes.Password or GrantTypes.RefreshToken or GrantTypes.Google)
            .WithMessage("grantType must be password, refresh_token or google.");
        When(x => x.GrantType == GrantTypes.Password, () =>
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty();
        });
        When(x => x.GrantType == GrantTypes.RefreshToken, () => RuleFor(x => x.RefreshToken).NotEmpty());
        When(x => x.GrantType == GrantTypes.Google, () => RuleFor(x => x.IdToken).NotEmpty());
    }
}
