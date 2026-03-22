using System.Reflection;
using NetArchTest.Rules;

namespace Tests.Architecture;

/// <summary>
/// Enforces Constitution Principle II: Module Isolation.
/// No module project may reference another module project directly;
/// the only allowed cross-module dependency is Shared.Contracts.
/// </summary>
public class ModuleIsolationTests
{
    private static readonly string[] Modules =
    [
        "Identity", "Messaging", "Presence", "Reactions",
        "Search", "Files", "Notifications", "RealTime", "Admin",
    ];

    /// <summary>
    /// Assemblies that must be present on disk (and therefore testable).
    /// Omits layers with no source files (e.g. Presence.Domain, Files.Domain,
    /// Reactions.Domain, Search.Domain — all empty csproj stubs).
    /// </summary>
    private static readonly string[] RequiredAssemblies =
    [
        // Domain (stubs with no types are intentionally excluded)
        "Identity.Domain", "Messaging.Domain", "Notifications.Domain",
        "RealTime.Domain", "Admin.Domain",

        // Application (stubs with no types are intentionally excluded)
        "Identity.Application", "Messaging.Application", "Reactions.Application",
        "Search.Application", "Notifications.Application", "RealTime.Application",
        "Admin.Application",

        // API — all 9 modules have an API layer
        "Identity.API", "Messaging.API", "Presence.API", "Reactions.API",
        "Search.API", "Files.API", "Notifications.API", "RealTime.API", "Admin.API",

        // Infrastructure — all 9 modules have an Infrastructure layer
        "Identity.Infrastructure", "Messaging.Infrastructure", "Presence.Infrastructure",
        "Reactions.Infrastructure", "Search.Infrastructure", "Files.Infrastructure",
        "Notifications.Infrastructure", "RealTime.Infrastructure", "Admin.Infrastructure",
    ];

    /// <summary>
    /// Loads all module assemblies directly from the test output directory.
    /// Using Assembly.LoadFrom is more reliable than AppDomain.CurrentDomain.GetAssemblies()
    /// because the latter only contains lazily-loaded entries.
    /// </summary>
    private static IReadOnlyList<Assembly> LoadAllModuleAssemblies()
    {
        var baseDir  = AppDomain.CurrentDomain.BaseDirectory;
        var result   = new List<Assembly>();

        foreach (var dll in Directory.GetFiles(baseDir, "*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(dll);
            if (Modules.Any(m => name.StartsWith(m + ".", StringComparison.Ordinal)))
            {
                try { result.Add(Assembly.LoadFrom(dll)); }
                catch { /* skip unloadable (e.g. native) dlls */ }
            }
        }

        return result;
    }

    /// <summary>
    /// Guard: verifies every expected assembly is present in the output directory.
    /// Catches the case where a new module is added to the Modules array but its
    /// csproj reference is missing from Tests.Architecture.csproj.
    /// </summary>
    [Fact]
    public void All_expected_module_assemblies_are_present()
    {
        var assemblies  = LoadAllModuleAssemblies();
        var loadedNames = assemblies.Select(a => a.GetName().Name).ToHashSet(StringComparer.Ordinal);
        var missing     = RequiredAssemblies.Where(name => !loadedNames.Contains(name)).ToList();

        Assert.True(missing.Count == 0,
            "The following assemblies were not found in the output directory — " +
            "add the missing ProjectReference to Tests.Architecture.csproj: " +
            string.Join(", ", missing));
    }

    [Fact]
    public void Domain_Layer_Must_Not_Reference_Other_Module_Assemblies()
    {
        var assemblies = LoadAllModuleAssemblies();

        foreach (var module in Modules)
        {
            var assembly = assemblies.FirstOrDefault(a => a.GetName().Name == $"{module}.Domain");
            if (assembly is null) continue; // module has no Domain layer (empty stub)

            var forbidden = Modules
                .Where(m => m != module)
                .SelectMany(m => new[] { $"{m}.Domain", $"{m}.Application", $"{m}.Infrastructure", $"{m}.API" })
                .ToArray();

            var result = Types
                .InAssembly(assembly)
                .ShouldNot().HaveDependencyOnAny(forbidden)
                .GetResult();

            Assert.True(result.IsSuccessful,
                $"{module}.Domain must not reference other module assemblies. " +
                $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Fact]
    public void Application_Layer_Must_Not_Reference_Other_Module_Infrastructure_Or_API()
    {
        var assemblies = LoadAllModuleAssemblies();

        foreach (var module in Modules)
        {
            var assembly = assemblies.FirstOrDefault(a => a.GetName().Name == $"{module}.Application");
            if (assembly is null) continue; // module has no Application layer (empty stub)

            var forbidden = Modules
                .Where(m => m != module)
                .SelectMany(m => new[] { $"{m}.Infrastructure", $"{m}.API" })
                .ToArray();

            var result = Types
                .InAssembly(assembly)
                .ShouldNot().HaveDependencyOnAny(forbidden)
                .GetResult();

            Assert.True(result.IsSuccessful,
                $"{module}.Application must not reference other module Infrastructure or API. " +
                $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Fact]
    public void API_Layer_Must_Not_Reference_Other_Module_Assemblies()
    {
        var assemblies = LoadAllModuleAssemblies();

        foreach (var module in Modules)
        {
            var assembly = assemblies.FirstOrDefault(a => a.GetName().Name == $"{module}.API");
            if (assembly is null) continue; // module has no API layer

            var forbidden = Modules
                .Where(m => m != module)
                .SelectMany(m => new[] { $"{m}.Domain", $"{m}.Application", $"{m}.Infrastructure", $"{m}.API" })
                .ToArray();

            var result = Types
                .InAssembly(assembly)
                .ShouldNot().HaveDependencyOnAny(forbidden)
                .GetResult();

            Assert.True(result.IsSuccessful,
                $"{module}.API must not reference other module assemblies. " +
                $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Fact]
    public void Infrastructure_Layer_Must_Not_Reference_Other_Module_Assemblies()
    {
        var assemblies = LoadAllModuleAssemblies();

        foreach (var module in Modules)
        {
            var assembly = assemblies.FirstOrDefault(a => a.GetName().Name == $"{module}.Infrastructure");
            if (assembly is null) continue; // module has no Infrastructure layer

            var forbidden = Modules
                .Where(m => m != module)
                .SelectMany(m => new[] { $"{m}.Domain", $"{m}.Application", $"{m}.Infrastructure", $"{m}.API" })
                .ToArray();

            var result = Types
                .InAssembly(assembly)
                .ShouldNot().HaveDependencyOnAny(forbidden)
                .GetResult();

            Assert.True(result.IsSuccessful,
                $"{module}.Infrastructure must not reference other module assemblies. " +
                $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }
}
