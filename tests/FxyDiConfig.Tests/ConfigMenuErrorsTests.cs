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
}
