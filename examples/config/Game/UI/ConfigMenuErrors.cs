using System;
using Godot;

namespace ConfigExample.Game;

internal static class ConfigMenuErrors
{
    public static void ThrowIfDirectoryCreationFailed(string settingsDirectory, Error error)
    {
        if (error != Error.Ok)
        {
            throw new InvalidOperationException(
                $"Could not create settings folder '{settingsDirectory}': {error}");
        }
    }

    public static void ThrowIfShellOpenFailed(string settingsDirectory, Error error)
    {
        if (error != Error.Ok)
        {
            throw new InvalidOperationException(
                $"Could not open settings folder '{settingsDirectory}': {error}");
        }
    }
}
