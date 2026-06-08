using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace Fxyoge.DependencyInjection;

internal static class StartupDiscovery
{
    public static IReadOnlyList<IStartup> CreateStartups()
    {
        var startupTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(GetStartupTypes)
            .OrderBy(type => type.Assembly.FullName, StringComparer.Ordinal)
            .ThenBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        var startups = new List<IStartup>(startupTypes.Length);

        foreach (var startupType in startupTypes)
        {
            try
            {
                if (Activator.CreateInstance(startupType) is not IStartup startup)
                {
                    throw new InvalidOperationException(
                        $"Startup type '{startupType.FullName}' did not create an IStartup instance.");
                }

                startups.Add(startup);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Could not create startup type '{startupType.FullName}'. Startup types must have a public parameterless constructor.",
                    ex);
            }
        }

        return startups;
    }

    internal static IEnumerable<Type> GetStartupTypes(Assembly assembly)
    {
        Type[] types;

        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            var loaderMessages = string.Join(
                System.Environment.NewLine,
                ex.LoaderExceptions
                    .Where(loaderException => loaderException is not null)
                    .Select(loaderException => loaderException!.Message));

            throw new InvalidOperationException(
                $"Could not scan assembly '{assembly.FullName}' for startup types because one or more types could not be loaded.{System.Environment.NewLine}{loaderMessages}",
                ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not scan assembly '{assembly.FullName}' for startup types.",
                ex);
        }

        return types.Where(IsStartupType);
    }

    private static bool IsStartupType(Type type)
        => typeof(IStartup).IsAssignableFrom(type)
            && type is { IsAbstract: false, IsInterface: false }
            && !type.ContainsGenericParameters;
}
