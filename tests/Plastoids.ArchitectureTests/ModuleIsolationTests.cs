using System.Reflection;
using NetArchTest.Rules;

namespace Plastoids.ArchitectureTests;

/// <summary>Enforces docs/Build_Prompt.md §2.5 (vertical slice rules) and §2.7 (only Infrastructure talks to Kafka).</summary>
public class ModuleIsolationTests
{
    private static readonly Assembly Api = typeof(Plastoids.Api.Program).Assembly;
    private static readonly Assembly Infrastructure = typeof(Plastoids.Infrastructure.InfrastructureServiceCollectionExtensions).Assembly;

    private static List<Assembly> ModuleAssemblies()
    {
        _ = Api; // force load of referenced module assemblies
        return AppDomain.CurrentDomain.GetAssemblies()
            .Concat(Api.GetReferencedAssemblies().Select(Assembly.Load))
            .Where(a => a.GetName().Name!.StartsWith("Plastoids.Modules.", StringComparison.Ordinal))
            .DistinctBy(a => a.FullName)
            .ToList();
    }

    private static string ModuleNamespace(Assembly a) => a.GetName().Name!; // Plastoids.Modules.<Name>

    [Fact]
    public void At_least_one_module_is_registered()
    {
        ModuleAssemblies().Should().NotBeEmpty();
    }

    [Fact]
    public void Features_only_use_other_modules_through_their_Contracts()
    {
        foreach (var module in ModuleAssemblies())
        {
            var others = ModuleAssemblies().Where(m => m != module).ToList();
            foreach (var other in others)
            {
                var otherNs = ModuleNamespace(other);
                // NetArchTest matches dependency namespaces by prefix, so the module root (which only holds the
                // <Name>Module class) must be left out or every Contracts reference would be flagged.
                var forbidden = other.GetTypes()
                    .Where(t => t.Namespace is not null && t.Namespace.StartsWith(otherNs + ".", StringComparison.Ordinal) && !t.Namespace.StartsWith(otherNs + ".Contracts", StringComparison.Ordinal))
                    .Select(t => t.Namespace!)
                    .Distinct()
                    .ToArray();
                if (forbidden.Length == 0)
                {
                    continue;
                }

                var result = Types.InAssembly(module)
                    .That().ResideInNamespace(ModuleNamespace(module))
                    .ShouldNot().HaveDependencyOnAny(forbidden)
                    .GetResult();

                result.IsSuccessful.Should().BeTrue($"{module.GetName().Name} must only reference {otherNs}.Contracts; offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
            }
        }
    }

    [Fact]
    public void Slices_do_not_reference_sibling_slices()
    {
        foreach (var module in ModuleAssemblies())
        {
            var featuresRoot = ModuleNamespace(module) + ".Features.";
            var sliceNamespaces = module.GetTypes()
                .Where(t => t.Namespace is not null && t.Namespace.StartsWith(featuresRoot, StringComparison.Ordinal))
                .Select(t => t.Namespace!)
                .Distinct()
                .ToList();

            foreach (var slice in sliceNamespaces)
            {
                // A sibling is another slice namespace that is not an ancestor or descendant of this one.
                var siblings = sliceNamespaces
                    .Where(s => s != slice && !s.StartsWith(slice + ".", StringComparison.Ordinal) && !slice.StartsWith(s + ".", StringComparison.Ordinal))
                    .ToArray();
                if (siblings.Length == 0)
                {
                    continue;
                }

                var result = Types.InAssembly(module)
                    .That().ResideInNamespace(slice)
                    .ShouldNot().HaveDependencyOnAny(siblings)
                    .GetResult();

                result.IsSuccessful.Should().BeTrue($"slice {slice} must not depend on sibling slices; offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
            }
        }
    }

    [Fact]
    public void Only_Infrastructure_touches_Kafka()
    {
        foreach (var assembly in ModuleAssemblies().Append(Api))
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot().HaveDependencyOn("Confluent.Kafka")
                .GetResult();
            result.IsSuccessful.Should().BeTrue($"{assembly.GetName().Name} must not use Confluent.Kafka directly; offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Fact]
    public void Modules_do_not_depend_on_the_host()
    {
        foreach (var module in ModuleAssemblies())
        {
            Types.InAssembly(module).ShouldNot().HaveDependencyOn("Plastoids.Api").GetResult().IsSuccessful.Should().BeTrue();
        }

        Types.InAssembly(Infrastructure).ShouldNot().HaveDependencyOn("Plastoids.Modules").GetResult().IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Handlers_and_validators_are_internal()
    {
        foreach (var module in ModuleAssemblies())
        {
            var result = Types.InAssembly(module)
                .That().ResideInNamespace(ModuleNamespace(module) + ".Features")
                .And().HaveNameEndingWith("Handler").Or().HaveNameEndingWith("Validator")
                .Should().NotBePublic()
                .GetResult();
            result.IsSuccessful.Should().BeTrue($"handlers/validators in {module.GetName().Name} should be internal; offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }
}
