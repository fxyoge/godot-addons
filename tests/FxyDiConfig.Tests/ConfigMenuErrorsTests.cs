extern alias ConfigExample;

using System;
using ConfigExample::ConfigExample.Game;
using Godot;
using Xunit;

namespace FxyDiConfig.Tests;

public sealed class ConfigMenuErrorsTests
{
    [Fact]
    public void ThrowIfDirectoryCreationFailedThrowsWhenDirectoryCannotBeCreated()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ConfigMenuErrors.ThrowIfDirectoryCreationFailed(
                "/settings",
                Error.FileNoPermission));

        Assert.Contains("/settings", ex.Message);
        Assert.Contains(nameof(Error.FileNoPermission), ex.Message);
    }

    [Fact]
    public void ThrowIfShellOpenFailedThrowsWhenFolderCannotBeOpened()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ConfigMenuErrors.ThrowIfShellOpenFailed(
                "/settings",
                Error.FileNotFound));

        Assert.Contains("/settings", ex.Message);
        Assert.Contains(nameof(Error.FileNotFound), ex.Message);
    }
}
