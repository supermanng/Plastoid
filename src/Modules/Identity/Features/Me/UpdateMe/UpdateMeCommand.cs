using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Identity.Features.Me.UpdateMe;

/// <summary>PATCH semantics: null fields are left unchanged.</summary>
public sealed record UpdateMeCommand(string? DisplayName, string? Email) : ICommand;
