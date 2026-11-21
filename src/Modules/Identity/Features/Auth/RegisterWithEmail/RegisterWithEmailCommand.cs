using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Identity.Features.Auth.RegisterWithEmail;

/// <summary>Adds email + password sign-in to the verified phone account of the caller.</summary>
public sealed record RegisterWithEmailCommand(string Email, string Password) : ICommand;
