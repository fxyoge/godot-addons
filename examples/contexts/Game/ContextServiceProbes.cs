using System;
using System.Collections.Generic;

namespace ContextsExample.Game;

internal static class ContextServiceProbes
{
    public static readonly ServiceProbe[] All =
    [
        new("controller", typeof(VehicleController)),
        new("input", typeof(IInputSource)),
        new("telemetry", typeof(IRunTelemetry)),
        new("track", typeof(ITrackSession)),
        new("surface", typeof(ITrackSurface)),
        new("modifiers", typeof(IEnumerable<ICarModifier>)),
        new("camera", typeof(ICameraRig)),
    ];
}

internal readonly record struct ServiceProbe(string Label, Type Type);
