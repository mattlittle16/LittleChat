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
        "Search", "Files", "Notifications", "RealTime",
    ];

    /// <summary>
    /// Returns all module Domain/Application assemblies loaded in the current AppDomain.
    /// Anchor types are touched to ensure their assemblies are loaded before AppDomain inspection.
    /// </summary>
    private static IReadOnlyList<Assembly> LoadModuleAssemblies()
    {
        // Force-load non-empty module assemblies via anchor types
        _ = typeof(Identity.Domain.User);
        _ = typeof(Identity.Application.UserSyncService);
        _ = typeof(Messaging.Domain.Message);
        _ = typeof(Messaging.Application.Commands.SendMessageCommand);
        _ = typeof(RealTime.Domain.IChatHubClient);
        _ = typeof(RealTime.Application.Handlers.MessageSentHandler);
        _ = typeof(Reactions.Application.IReactionRepository);
        _ = typeof(Search.Application.Queries.SearchQuery);
        _ = typeof(Notifications.Application.Handlers.UserMentionedHandler);

        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => Modules.Any(m => a.GetName().Name?.StartsWith(m + ".") == true))
            .ToList();
    }

    [Fact]
    public void Domain_Layer_Must_Not_Reference_Other_Module_Assemblies()
    {
        var assemblies = LoadModuleAssemblies();

        foreach (var module in Modules)
        {
            var assembly = assemblies.FirstOrDefault(a => a.GetName().Name == $"{module}.Domain");
            if (assembly is null) continue;

            var forbidden = Modules
                .Where(m => m != module)
                .SelectMany(m => new[]
                {
                    $"{m}.Domain", $"{m}.Application", $"{m}.Infrastructure", $"{m}.API",
                })
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
        var assemblies = LoadModuleAssemblies();

        foreach (var module in Modules)
        {
            var assembly = assemblies.FirstOrDefault(a => a.GetName().Name == $"{module}.Application");
            if (assembly is null) continue;

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
}
