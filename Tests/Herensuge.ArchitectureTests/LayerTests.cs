using System.Reflection;
using NetArchTest.Rules;

namespace Herensuge.ArchitectureTests;

/// <summary>Clean Architecture dependency rule and module isolation (RESUMEN 6.2 and 6.3).</summary>
public sealed class LayerTests
{
    private static readonly string[] ModuleNames = ["Identity", "Tenancy", "Oidc", "Audit", "Notifications"];

    private static readonly string[] Frameworks =
        ["Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "OpenIddict", "StackExchange.Redis", "Fido2NetLib", "OtpNet", "Konscious"];

    public static TheoryData<string> Modules => [.. ModuleNames];

    [Theory]
    [MemberData(nameof(Modules))]
    public void Domain_depends_on_nothing_but_the_common_domain(string module)
    {
        if (Load(module, "Domain") is not { } domain)
        {
            return;
        }

        AssertNoDependency(domain, [.. Frameworks, $"Herensuge.Modules.{module}.Application", $"Herensuge.Modules.{module}.Infrastructure", $"Herensuge.Modules.{module}.Presentation", "Herensuge.Common.Application", "Herensuge.Common.Infrastructure"]);
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Application_does_not_depend_on_infrastructure_or_web_frameworks(string module) =>
        AssertNoDependency(Load(module, "Application")!, [.. Frameworks, $"Herensuge.Modules.{module}.Infrastructure", $"Herensuge.Modules.{module}.Presentation", "Herensuge.Common.Infrastructure", "Herensuge.Common.Presentation"]);

    [Theory]
    [MemberData(nameof(Modules))]
    public void Presentation_does_not_depend_on_infrastructure(string module)
    {
        if (Load(module, "Presentation") is not { } presentation)
        {
            return;
        }

        AssertNoDependency(presentation, [$"Herensuge.Modules.{module}.Infrastructure", "Herensuge.Common.Infrastructure", "Microsoft.EntityFrameworkCore", "Npgsql"]);
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Modules_only_see_the_contracts_of_other_modules(string module)
    {
        string[] forbidden =
        [
            .. ModuleNames
                .Where(other => other != module)
                .SelectMany(other => new[] { "Domain", "Application", "Infrastructure", "Presentation" }.Select(layer => $"Herensuge.Modules.{other}.{layer}")),
        ];

        foreach (string layer in new[] { "Domain", "Application", "Infrastructure", "Presentation", "Contracts" })
        {
            if (Load(module, layer) is { } assembly)
            {
                AssertNoDependency(assembly, forbidden);
            }
        }
    }

    [Theory]
    [InlineData("Identity")]
    [InlineData("Tenancy")]
    public void Contracts_are_framework_free(string module) =>
        AssertNoDependency(Load(module, "Contracts")!, [.. Frameworks, $"Herensuge.Modules.{module}.Domain", $"Herensuge.Modules.{module}.Application"]);

    [Fact]
    public void Common_domain_and_application_are_framework_free()
    {
        AssertNoDependency(Assembly.Load("Herensuge.Common.Domain"), [.. Frameworks, "Herensuge.Common.Application", "Herensuge.Common.Infrastructure"]);
        AssertNoDependency(Assembly.Load("Herensuge.Common.Application"), [.. Frameworks, "Herensuge.Common.Infrastructure", "Herensuge.Common.Presentation"]);
    }

    [Fact]
    public void Rule_engine_sees_real_dependencies()
    {
        // Positive control: if this fails, every "should not depend" test above would pass vacuously.
        NetArchTest.Rules.TestResult result = Types.InAssembly(Load("Identity", "Infrastructure")!)
            .That().HaveNameEndingWith("Repository")
            .Should().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    private static Assembly? Load(string module, string layer)
    {
        try
        {
            return Assembly.Load($"Herensuge.Modules.{module}.{layer}");
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static void AssertNoDependency(Assembly assembly, string[] forbidden)
    {
        NetArchTest.Rules.TestResult result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        Assert.True(
            result.IsSuccessful,
            $"{assembly.GetName().Name} must not depend on {string.Join(", ", forbidden)}. Offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
