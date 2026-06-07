using Fxyoge.DependencyInjection.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Xunit;

namespace FxyDiConfig.Tests;

public sealed class SettingsMonitorTests
{
    [Fact]
    public void CurrentValueLoadsDefaults()
    {
        var services = CreateServices(store: out _);

        var options = services.GetRequiredService<ISettingsMonitor<TestOptions>>().CurrentValue;

        Assert.Equal(0.75f, options.Volume);
        Assert.False(options.Muted);
        Assert.Equal("Normal", options.Difficulty);
    }

    [Fact]
    public async Task UpdateStoresAppliesSavesAndNotifies()
    {
        var services = CreateServices(store: out var store);
        var runtime = services.GetRequiredService<TestRuntimeBinding<float>>();
        var monitor = services.GetRequiredService<ISettingsMonitor<TestOptions>>();
        var notificationCount = 0;

        monitor.OnChange(options =>
        {
            notificationCount++;
            Assert.Equal(0.25f, options.Volume);
        });

        await monitor.Update(options => options.Volume = 0.25f);

        Assert.True(store.TryGet<float>("settings", "volume", out var stored));
        Assert.Equal(0.25f, stored);
        Assert.Equal(0.25f, runtime.AppliedValue);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(1, notificationCount);
    }

    [Fact]
    public async Task SessionApplyDoesNotSaveUntilSessionSave()
    {
        var services = CreateServices(store: out var store);
        var runtime = services.GetRequiredService<TestRuntimeBinding<float>>();
        var monitor = services.GetRequiredService<ISettingsMonitor<TestOptions>>();
        var session = monitor.CreateSession();

        session.Value.Volume = 0.5f;
        await session.Apply();

        Assert.Equal(0.5f, runtime.AppliedValue);
        Assert.Equal(0, store.SaveCount);

        session.Value.Volume = 0.6f;
        await session.Save();

        Assert.Equal(0.6f, runtime.AppliedValue);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task ResetRemovesOverlayAndReloadsRuntimeDefaults()
    {
        var services = CreateServices(store: out var store);
        var runtime = services.GetRequiredService<TestRuntimeBinding<float>>();
        var monitor = services.GetRequiredService<ISettingsMonitor<TestOptions>>();

        await monitor.Update(options => options.Volume = 0.2f);
        runtime.DefaultValue = 0.9f;

        await monitor.Reset();

        Assert.False(store.TryGet<float>("settings", "volume", out _));
        Assert.Equal(0.9f, monitor.CurrentValue.Volume);
        Assert.Equal(0.9f, runtime.AppliedValue);
    }

    [Fact]
    public async Task InputActionBindingsUseNumberedBindingPaths()
    {
        var store = new MemoryConfigOverlayStore();
        var services = new ServiceCollection();
        var runtime = new TestRuntimeBinding<InputActionBindings>(InputActionBindings.FromKeyCode(32));

        services.AddSingleton<IConfigOverlayStore>(store);
        services.AddSettings<InputTestOptions>("input", input =>
        {
            input.Map(x => x.Jump)
                .PersistAs("jump")
                .ToRuntime(
                    runtime,
                    InputActionBindings.FromKeyCode(32),
                    InputActionBindingConfigValueCodec.Instance);
        });

        var monitor = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        }).GetRequiredService<ISettingsMonitor<InputTestOptions>>();

        await monitor.Update(options =>
        {
            options.Jump = new InputActionBindings(new InputBinding[]
            {
                new KeyInputBinding(74, "J", Shift: true),
                new MouseButtonInputBinding(1, "Left Mouse"),
            });
        });

        Assert.True(store.TryGet<string>("input", "jump/0/type", out var firstType));
        Assert.Equal("key", firstType);
        Assert.True(store.TryGet<long>("input", "jump/0/key_code", out var storedKeyCode));
        Assert.Equal(74, storedKeyCode);
        Assert.True(store.TryGet<bool>("input", "jump/0/shift", out var storedShift));
        Assert.True(storedShift);
        Assert.True(store.TryGet<string>("input", "jump/1/type", out var secondType));
        Assert.Equal("mouse_button", secondType);
        Assert.True(store.TryGet<long>("input", "jump/1/button_index", out var storedButtonIndex));
        Assert.Equal(1, storedButtonIndex);

        await monitor.Update(options => options.Jump = InputActionBindings.Empty);

        Assert.True(store.TryGet<string>("input", "jump/0/type", out var emptyType));
        Assert.Equal("none", emptyType);
        Assert.False(store.TryGet<string>("input", "jump/1/type", out _));
    }

    [Fact]
    public void CurrentValueCopiesMappedValuesByAssignment()
    {
        var store = new MemoryConfigOverlayStore();
        var services = new ServiceCollection();
        var runtime = new TestRuntimeBinding<InputActionBindings>(InputActionBindings.FromKeyCode(32));

        services.AddSingleton<IConfigOverlayStore>(store);
        services.AddSettings<InputTestOptions>("input", input =>
        {
            input.Map(x => x.Jump)
                .PersistAs("jump")
                .ToRuntime(
                    runtime,
                    InputActionBindings.FromKeyCode(32),
                    InputActionBindingConfigValueCodec.Instance);
        });

        var monitor = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        }).GetRequiredService<ISettingsMonitor<InputTestOptions>>();

        var first = monitor.CurrentValue;
        var second = monitor.CurrentValue;

        Assert.NotSame(first, second);
        Assert.Equal(first.Jump, second.Jump);
    }

    [Fact]
    public void AddSettingsRejectsMutableReferenceMappedValues()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<NotSupportedException>(() =>
        {
            services.AddSettings<MutableReferenceOptions>("mutable", settings =>
            {
                settings.Map(x => x.Value)
                    .ToUserConfig(new MutableReferenceValue());
            });
        });

        Assert.Contains("mutable/value", ex.Message);
        Assert.Contains("implement ICloneable", ex.Message);
    }

    [Fact]
    public void CurrentValueClonesICloneableMappedReferenceValues()
    {
        var services = new ServiceCollection();
        var runtime = new TestRuntimeBinding<CloneableReferenceValue>(new CloneableReferenceValue(10));

        services.AddSingleton<IConfigOverlayStore>(new MemoryConfigOverlayStore());
        services.AddSingleton(runtime);
        services.AddSettings<CloneableReferenceOptions>("cloneable", settings =>
        {
            settings.Map(x => x.Value)
                .ToRuntime(runtime, new CloneableReferenceValue(10));
        });

        var monitor = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        }).GetRequiredService<ISettingsMonitor<CloneableReferenceOptions>>();

        var first = monitor.CurrentValue;
        first.Value.Amount = 20;

        Assert.Equal(10, monitor.CurrentValue.Value.Amount);
        Assert.NotSame(first.Value, monitor.CurrentValue.Value);
    }

    [Fact]
    public void ProjectSettingBindingDescribesRuntimeMutability()
    {
        var binding = new GodotProjectSettingBinding<string>(
            "application/config/name",
            "Game",
            runtimeMutable: false,
            requiresRestart: true);

        var descriptor = binding.Describe("project", "game_title", uiHint: null);

        Assert.Equal(ConfigValueSource.ProjectSettings, descriptor.Source);
        Assert.True(descriptor.Writable);
        Assert.False(descriptor.RuntimeMutable);
        Assert.True(descriptor.RequiresRestart);
    }

    private static ServiceProvider CreateServices(out MemoryConfigOverlayStore store)
    {
        store = new MemoryConfigOverlayStore();
        var capturedStore = store;
        var services = new ServiceCollection();
        var runtime = new TestRuntimeBinding<float>(0.75f);

        services.AddSingleton(capturedStore);
        services.AddSingleton<IConfigOverlayStore>(capturedStore);
        services.AddSingleton(runtime);
        services.AddSettings<TestOptions>("settings", settings =>
        {
            settings.Map(x => x.Volume)
                .WithUi("Volume", ConfigUiControl.Slider, 0, 1, 0.01)
                .ToRuntime(runtime, 0.75f);

            settings.Map(x => x.Muted)
                .WithUi("Mute", ConfigUiControl.Toggle)
                .ToUserConfig(false);

            settings.Map(x => x.Difficulty)
                .WithUi("Difficulty", ConfigUiControl.Select)
                .ToUserConfig("Normal");
        });

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    private sealed class TestOptions
    {
        public float Volume { get; set; }

        public bool Muted { get; set; }

        public string Difficulty { get; set; } = string.Empty;
    }

    private sealed class InputTestOptions
    {
        public InputActionBindings Jump { get; set; } = new();
    }

    private sealed class MutableReferenceOptions
    {
        public MutableReferenceValue Value { get; set; } = new();
    }

    private sealed class MutableReferenceValue
    {
    }

    private sealed class CloneableReferenceOptions
    {
        public CloneableReferenceValue Value { get; set; } = new(0);
    }

    private sealed class CloneableReferenceValue : ICloneable
    {
        public CloneableReferenceValue(int amount)
        {
            Amount = amount;
        }

        public int Amount { get; set; }

        public object Clone() => new CloneableReferenceValue(Amount);
    }

    private sealed class TestRuntimeBinding<TValue> : IRuntimeConfigBinding<TValue>
    {
        public TestRuntimeBinding(TValue defaultValue)
        {
            DefaultValue = defaultValue;
            AppliedValue = defaultValue;
        }

        public TValue DefaultValue { get; set; }

        public TValue AppliedValue { get; private set; }

        public TValue ReadDefault() => DefaultValue;

        public void Apply(TValue value)
        {
            AppliedValue = value;
        }

        public ConfigEntryDescriptor Describe(string section, string key, ConfigUiHint? uiHint)
            => new(section, key, typeof(TValue), ConfigValueSource.Runtime, true, true, false, uiHint);
    }
}
