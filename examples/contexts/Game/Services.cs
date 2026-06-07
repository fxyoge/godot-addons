using System;
using System.Collections.Generic;
using System.Linq;

namespace ContextsExample.Game;

public interface ISettingsStore
{
    string Label { get; }
}

public sealed class ProjectSettingsStore : ISettingsStore
{
    public string Label { get; } = $"project/{Ids.New()}";
}

public sealed class UserSettingsStore : ISettingsStore
{
    public string Label { get; } = $"user/{Ids.New()}";
}

public sealed class PreviewSettingsStore : ISettingsStore
{
    public string Label { get; } = $"preview/{Ids.New()}";
}

public sealed class ReplaySettingsStore : ISettingsStore
{
    public string Label { get; } = $"replay/{Ids.New()}";
}

public sealed class SettingsManager
{
    public SettingsManager(IEnumerable<ISettingsStore> stores)
    {
        Stores = stores.ToArray();
        Id = Ids.New();
    }

    public string Id { get; }

    public IReadOnlyList<ISettingsStore> Stores { get; }
}

public interface ILevelSession
{
    string Id { get; }
}

public sealed class LevelSession : ILevelSession
{
    public string Id { get; } = Ids.New();
}

public interface IPlayerProfile
{
    string Id { get; }

    string LevelSessionId { get; }
}

public sealed class PlayerProfile : IPlayerProfile
{
    public PlayerProfile(ILevelSession levelSession)
    {
        Id = Ids.New();
        LevelSessionId = levelSession.Id;
    }

    public string Id { get; }

    public string LevelSessionId { get; }
}

public interface IControlMode
{
    string Label { get; }
}

public sealed class RuntimeControlMode : IControlMode
{
    public string Label { get; } = $"runtime/{Ids.New()}";
}

public sealed class PreviewControlMode : IControlMode
{
    public string Label { get; } = $"preview/{Ids.New()}";
}

public sealed class ReplayControlMode : IControlMode
{
    public string Label { get; } = $"replay/{Ids.New()}";
}

public interface IDebugOverlayModel
{
    string Id { get; }
}

public sealed class DebugOverlayModel : IDebugOverlayModel
{
    public string Id { get; } = Ids.New();
}

public interface ICameraRig
{
    string Id { get; }
}

public sealed class PreviewCameraRig : ICameraRig
{
    public string Id { get; } = Ids.New();
}

internal static class Ids
{
    public static string New() => Guid.NewGuid().ToString("N")[..6];
}
