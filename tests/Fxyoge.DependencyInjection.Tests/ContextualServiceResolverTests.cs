using System;
using System.Collections.Generic;
using System.Linq;
using Fxyoge.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fxyoge.DependencyInjection.Tests;

public sealed class ContextualServiceResolverTests
{
    [Fact]
    public void ExactRuleMatchesPresentGroup()
    {
        using var harness = new Harness(services =>
            services.AddContextualScoped<IAssetLoader, PreviewAssetLoader>("preview"));

        var service = harness.Resolve<IAssetLoader>("preview");

        Assert.IsType<PreviewAssetLoader>(service);
    }

    [Fact]
    public void ExactRuleDoesNotMatchMissingGroup()
    {
        using var harness = new Harness(services =>
            services.AddContextualScoped<IAssetLoader, PreviewAssetLoader>("preview"));

        var service = harness.ResolveOrNull<IAssetLoader>("runtime");

        Assert.Null(service);
    }

    [Fact]
    public void NegativeRuleMatchesWhenGroupIsAbsent()
    {
        using var harness = new Harness(services =>
            services.AddContextualScoped<IAssetLoader, RuntimeAssetLoader>("!preview"));

        var service = harness.Resolve<IAssetLoader>("runtime");

        Assert.IsType<RuntimeAssetLoader>(service);
    }

    [Fact]
    public void NegativeRuleDoesNotMatchWhenGroupIsPresent()
    {
        using var harness = new Harness(services =>
            services.AddContextualScoped<IAssetLoader, RuntimeAssetLoader>("!preview"));

        var service = harness.ResolveOrNull<IAssetLoader>("preview");

        Assert.Null(service);
    }

    [Fact]
    public void WildcardRuleMatchesPrefixedGroup()
    {
        using var harness = new Harness(services =>
            services.AddContextualScoped<ILevelSession, LevelSession>("level:*"));

        var service = harness.Resolve<ILevelSession>("level:forest");

        Assert.IsType<LevelSession>(service);
    }

    [Fact]
    public void WildcardRuleDoesNotMatchDifferentPrefix()
    {
        using var harness = new Harness(services =>
            services.AddContextualScoped<ILevelSession, LevelSession>("level:*"));

        var service = harness.ResolveOrNull<ILevelSession>("match:forest");

        Assert.Null(service);
    }

    [Fact]
    public void NegativeWildcardRuleMatchesWhenNoPrefixedGroupExists()
    {
        using var harness = new Harness(services =>
            services.AddContextualScoped<ITransactionMode, NoTransactionMode>("!transaction:*"));

        var service = harness.Resolve<ITransactionMode>("preview");

        Assert.IsType<NoTransactionMode>(service);
    }

    [Fact]
    public void NegativeWildcardRuleDoesNotMatchWhenPrefixedGroupExists()
    {
        using var harness = new Harness(services =>
            services.AddContextualScoped<ITransactionMode, NoTransactionMode>("!transaction:*"));

        var service = harness.ResolveOrNull<ITransactionMode>("transaction:abc");

        Assert.Null(service);
    }

    [Fact]
    public void CompositeRuleRequiresEveryPositiveTerm()
    {
        using var harness = new Harness(services =>
            services.AddContextualScoped<ICameraRig, EditorCameraRig>("preview", "camera:*"));

        Assert.NotNull(harness.ResolveOrNull<ICameraRig>("preview", "camera:main"));
        Assert.Null(harness.ResolveOrNull<ICameraRig>("preview"));
        Assert.Null(harness.ResolveOrNull<ICameraRig>("camera:main"));
    }

    [Fact]
    public void CompositeRuleRejectsPresentNegativeTerm()
    {
        using var harness = new Harness(services =>
            services.AddContextualScoped<IAssetLoader, RuntimeAssetLoader>("!preview", "level:*"));

        Assert.NotNull(harness.ResolveOrNull<IAssetLoader>("runtime", "level:forest"));
        Assert.Null(harness.ResolveOrNull<IAssetLoader>("preview", "level:forest"));
    }

    [Fact]
    public void ScopedExactRuleSharesAcrossContextsWithExtraGroups()
    {
        using var harness = new Harness(services =>
            services.AddContextualScoped<IAssetLoader, PreviewAssetLoader>("preview"));

        var first = harness.Resolve<IAssetLoader>("preview", "transaction:abc");
        var second = harness.Resolve<IAssetLoader>("preview", "transaction:def");

        Assert.Same(first, second);
    }

    [Fact]
    public void ScopedWildcardRuleSplitsByMatchedGroupValue()
    {
        using var harness = new Harness(services =>
            services.AddContextualScoped<ITransactionStore, TransactionStore>("transaction:*"));

        var first = harness.Resolve<ITransactionStore>("preview", "transaction:abc");
        var second = harness.Resolve<ITransactionStore>("preview", "transaction:def");
        var firstAgain = harness.Resolve<ITransactionStore>("runtime", "transaction:abc");

        Assert.NotSame(first, second);
        Assert.Same(first, firstAgain);
    }

    [Fact]
    public void ScopedCompositeRuleSplitsByMatchedTuple()
    {
        using var harness = new Harness(services =>
            services.AddContextualScoped<IPlayerProfile, PlayerProfile>("level:*", "player:*"));

        var forestPlayerOne = harness.Resolve<IPlayerProfile>("level:forest", "player:1");
        var forestPlayerTwo = harness.Resolve<IPlayerProfile>("level:forest", "player:2");
        var forestPlayerOneAgain = harness.Resolve<IPlayerProfile>("preview", "level:forest", "player:1");

        Assert.NotSame(forestPlayerOne, forestPlayerTwo);
        Assert.Same(forestPlayerOne, forestPlayerOneAgain);
    }

    [Fact]
    public void ContextualSingletonIgnoresWildcardMatchedValue()
    {
        using var harness = new Harness(services =>
            services.AddContextualSingleton<ITransactionStore, TransactionStore>("transaction:*"));

        var first = harness.Resolve<ITransactionStore>("transaction:abc");
        var second = harness.Resolve<ITransactionStore>("transaction:def");

        Assert.Same(first, second);
    }

    [Fact]
    public void ContextualTransientCreatesNewInstanceEachTime()
    {
        using var harness = new Harness(services =>
            services.AddContextualTransient<IAssetLoader, PreviewAssetLoader>("preview"));

        var first = harness.Resolve<IAssetLoader>("preview");
        var second = harness.Resolve<IAssetLoader>("preview");

        Assert.NotSame(first, second);
    }

    [Fact]
    public void MoreSpecificContextualRegistrationWinsSingleResolution()
    {
        using var harness = new Harness(services =>
        {
            services.AddContextualScoped<IAssetLoader, PreviewAssetLoader>("preview");
            services.AddContextualScoped<IAssetLoader, LevelPreviewAssetLoader>("preview", "level:*");
        });

        var service = harness.Resolve<IAssetLoader>("preview", "level:forest");

        Assert.IsType<LevelPreviewAssetLoader>(service);
    }

    [Fact]
    public void ExactRuleBeatsWildcardRuleWhenTermCountTies()
    {
        using var harness = new Harness(services =>
        {
            services.AddContextualScoped<IModeService, WildcardModeService>("mode:*");
            services.AddContextualScoped<IModeService, ExactPreviewModeService>("mode:preview");
        });

        var service = harness.Resolve<IModeService>("mode:preview");

        Assert.IsType<ExactPreviewModeService>(service);
    }

    [Fact]
    public void AmbiguousContextualRegistrationsThrow()
    {
        using var harness = new Harness(services =>
        {
            services.AddContextualScoped<IAssetLoader, PreviewAssetLoader>("preview");
            services.AddContextualScoped<IAssetLoader, AlternatePreviewAssetLoader>("preview");
        });

        var ex = Assert.Throws<InvalidOperationException>(() => harness.Resolve<IAssetLoader>("preview"));
        Assert.Contains("ambiguous", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalTransientCanDependOnContextualSingleService()
    {
        using var harness = new Harness(services =>
        {
            services.AddTransient<AssetPresenter>();
            services.AddContextualScoped<IAssetLoader, PreviewAssetLoader>("preview");
            services.AddContextualScoped<IAssetLoader, RuntimeAssetLoader>("!preview");
        });

        var preview = harness.Resolve<AssetPresenter>("preview");
        var runtime = harness.Resolve<AssetPresenter>("runtime");

        Assert.IsType<PreviewAssetLoader>(preview.Loader);
        Assert.IsType<RuntimeAssetLoader>(runtime.Loader);
    }

    [Fact]
    public void NormalTransientFactoryCanDependOnContextualSingleService()
    {
        using var harness = new Harness(services =>
        {
            services.AddTransient(provider => new AssetPresenter(provider.GetRequiredService<IAssetLoader>()));
            services.AddContextualScoped<IAssetLoader, PreviewAssetLoader>("preview");
        });

        var presenter = harness.Resolve<AssetPresenter>("preview");

        Assert.IsType<PreviewAssetLoader>(presenter.Loader);
    }

    [Fact]
    public void ContextFlowsThroughNestedNormalTransientGraph()
    {
        using var harness = new Harness(services =>
        {
            services.AddTransient<Dashboard>();
            services.AddTransient<SettingsManager>();
            services.AddSingleton<ISettingsStore, ProjectSettingsStore>();
            services.AddContextualScoped<ISettingsStore, PreviewSettingsStore>("preview");
        });

        var dashboard = harness.Resolve<Dashboard>("preview");

        Assert.Contains(dashboard.Manager.Stores, store => store is ProjectSettingsStore);
        Assert.Contains(dashboard.Manager.Stores, store => store is PreviewSettingsStore);
    }

    [Fact]
    public void EnumerableResolutionCombinesNormalAndContextualServices()
    {
        using var harness = new Harness(services =>
        {
            services.AddSingleton<ISettingsStore, ProjectSettingsStore>();
            services.AddSingleton<ISettingsStore, UserSettingsStore>();
            services.AddContextualScoped<ISettingsStore, PreviewSettingsStore>("preview");
        });

        var stores = harness.Resolve<IEnumerable<ISettingsStore>>("preview").ToArray();

        Assert.Collection(
            stores,
            store => Assert.IsType<ProjectSettingsStore>(store),
            store => Assert.IsType<UserSettingsStore>(store),
            store => Assert.IsType<PreviewSettingsStore>(store));
    }

    [Fact]
    public void EnumerableResolutionOmitsUnmatchedContextualServices()
    {
        using var harness = new Harness(services =>
        {
            services.AddSingleton<ISettingsStore, ProjectSettingsStore>();
            services.AddContextualScoped<ISettingsStore, PreviewSettingsStore>("preview");
        });

        var stores = harness.Resolve<IEnumerable<ISettingsStore>>("runtime").ToArray();

        Assert.Single(stores);
        Assert.IsType<ProjectSettingsStore>(stores[0]);
    }

    [Fact]
    public void EnumerableResolutionIncludesAllMatchingContextualServices()
    {
        using var harness = new Harness(services =>
        {
            services.AddContextualScoped<ISettingsStore, PreviewSettingsStore>("preview");
            services.AddContextualScoped<ISettingsStore, LevelSettingsStore>("level:*");
        });

        var stores = harness.Resolve<IEnumerable<ISettingsStore>>("preview", "level:forest").ToArray();

        Assert.Contains(stores, store => store is PreviewSettingsStore);
        Assert.Contains(stores, store => store is LevelSettingsStore);
    }

    [Fact]
    public void ContextualServiceQueryReturnsAllMatchingContextualRegistrations()
    {
        using var harness = new Harness(services =>
        {
            services.AddSingleton<ISettingsStore, ProjectSettingsStore>();
            services.AddContextualScoped<ISettingsStore, PreviewSettingsStore>("preview");
            services.AddContextualTransient<ISettingsStore, LevelSettingsStore>("level:*");
            services.AddContextualScoped<IAssetLoader, RuntimeAssetLoader>("runtime");
        });

        var matches = harness.GetContextualServices("preview", "level:forest");

        Assert.Collection(
            matches,
            match =>
            {
                Assert.Equal(typeof(ISettingsStore), match.ServiceType);
                Assert.Equal(typeof(LevelSettingsStore), match.ImplementationType);
                Assert.Equal(ServiceLifetime.Transient, match.Lifetime);
                Assert.Equal("level:*", match.Rule);
            },
            match =>
            {
                Assert.Equal(typeof(ISettingsStore), match.ServiceType);
                Assert.Equal(typeof(PreviewSettingsStore), match.ImplementationType);
                Assert.Equal(ServiceLifetime.Scoped, match.Lifetime);
                Assert.Equal("preview", match.Rule);
            });
    }

    [Fact]
    public void SingleContextualResolutionFallsBackToNormalService()
    {
        using var harness = new Harness(services =>
            services.AddSingleton<IAssetLoader, RuntimeAssetLoader>());

        var service = harness.Resolve<IAssetLoader>("preview");

        Assert.IsType<RuntimeAssetLoader>(service);
    }

    [Fact]
    public void MatchingContextualServiceOverridesNormalServiceForSingleResolution()
    {
        using var harness = new Harness(services =>
        {
            services.AddSingleton<IAssetLoader, RuntimeAssetLoader>();
            services.AddContextualScoped<IAssetLoader, PreviewAssetLoader>("preview");
        });

        var service = harness.Resolve<IAssetLoader>("preview");

        Assert.IsType<PreviewAssetLoader>(service);
    }

    [Fact]
    public void RequiredMissingServiceThrows()
    {
        using var harness = new Harness(_ => { });

        Assert.Throws<InvalidOperationException>(() => harness.Resolve<IAssetLoader>("preview"));
    }

    [Fact]
    public void OptionalMissingServiceReturnsNull()
    {
        using var harness = new Harness(_ => { });

        Assert.Null(harness.ResolveOrNull<IAssetLoader>("preview"));
    }

    [Fact]
    public void ScopedContextualDisposablesAreDisposedWithResolver()
    {
        DisposableService.DisposeCount = 0;
        using (var harness = new Harness(services =>
            services.AddContextualScoped<IDisposableService, DisposableService>("preview")))
        {
            _ = harness.Resolve<IDisposableService>("preview");
        }

        Assert.Equal(1, DisposableService.DisposeCount);
    }

    [Fact]
    public void DisposeContextDoesNotDisposeSharedInferredPartitions()
    {
        DisposableService.DisposeCount = 0;
        using var harness = new Harness(services =>
            services.AddContextualScoped<IDisposableService, DisposableService>("preview"));

        _ = harness.Resolve<IDisposableService>("preview");
        harness.DisposeContext("preview");

        Assert.Equal(0, DisposableService.DisposeCount);
    }

    [Fact]
    public void ContextualTransientDisposablesAreDisposedWithResolver()
    {
        DisposableService.DisposeCount = 0;
        using (var harness = new Harness(services =>
            services.AddContextualTransient<IDisposableService, DisposableService>("preview")))
        {
            _ = harness.Resolve<IDisposableService>("preview");
            _ = harness.Resolve<IDisposableService>("preview");
        }

        Assert.Equal(2, DisposableService.DisposeCount);
    }

    [Fact]
    public void NormalTransientDisposablesCreatedContextuallyAreDisposedWithResolver()
    {
        DisposableShell.DisposeCount = 0;
        using (var harness = new Harness(services =>
        {
            services.AddTransient<DisposableShell>();
            services.AddContextualScoped<IAssetLoader, PreviewAssetLoader>("preview");
        }))
        {
            _ = harness.Resolve<DisposableShell>("preview");
            _ = harness.Resolve<DisposableShell>("preview");
        }

        Assert.Equal(2, DisposableShell.DisposeCount);
    }

    [Fact]
    public void RootSingletonCanStillUseNormalDependencies()
    {
        using var harness = new Harness(services =>
        {
            services.AddSingleton<ProjectSettingsStore>();
            services.AddSingleton<SingletonSettingsShell>();
        });

        var shell = harness.Resolve<SingletonSettingsShell>("preview");

        Assert.NotNull(shell.Store);
    }

    [Fact]
    public void RootSingletonDoesNotReceiveContextualDependencies()
    {
        using var harness = new Harness(services =>
        {
            services.AddSingleton<SingletonAssetShell>();
            services.AddContextualScoped<IAssetLoader, PreviewAssetLoader>("preview");
        });

        Assert.Throws<InvalidOperationException>(() => harness.Resolve<SingletonAssetShell>("preview"));
    }

    [Fact]
    public void InvalidEmptyRuleThrowsDuringRegistration()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddContextualScoped<IAssetLoader, PreviewAssetLoader>());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("!")]
    [InlineData("*")]
    [InlineData("transaction:*:bad")]
    public void InvalidRuleTermThrowsDuringRegistration(string rule)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddContextualScoped<IAssetLoader, PreviewAssetLoader>(rule));
    }

    [Fact]
    public void ContextualConstructorCanDependOnAnotherContextualService()
    {
        using var harness = new Harness(services =>
        {
            services.AddContextualScoped<IAssetLoader, PreviewAssetLoader>("preview");
            services.AddContextualScoped<IAssetPresenter, ContextualAssetPresenter>("preview");
        });

        var presenter = harness.Resolve<IAssetPresenter>("preview");

        Assert.IsType<PreviewAssetLoader>(presenter.Loader);
    }

    [Fact]
    public void ContextualDependencyUsesSameMatchedWildcardValue()
    {
        using var harness = new Harness(services =>
        {
            services.AddContextualScoped<ITransactionStore, TransactionStore>("transaction:*");
            services.AddContextualTransient<ITransactionPresenter, TransactionPresenter>("transaction:*");
        });

        var first = harness.Resolve<ITransactionPresenter>("transaction:abc");
        var second = harness.Resolve<ITransactionPresenter>("transaction:abc");
        var third = harness.Resolve<ITransactionPresenter>("transaction:def");

        Assert.Same(first.Store, second.Store);
        Assert.NotSame(first.Store, third.Store);
    }

    [Fact]
    public void NormalTransientFactoryCanDependOnContextualEnumerable()
    {
        using var harness = new Harness(services =>
        {
            services.AddSingleton<ISettingsStore, ProjectSettingsStore>();
            services.AddContextualScoped<ISettingsStore, PreviewSettingsStore>("preview");
            services.AddTransient(provider => new SettingsManager(provider.GetRequiredService<IEnumerable<ISettingsStore>>()));
        });

        var manager = harness.Resolve<SettingsManager>("preview");

        Assert.Equal(2, manager.Stores.Count);
        Assert.Contains(manager.Stores, store => store is ProjectSettingsStore);
        Assert.Contains(manager.Stores, store => store is PreviewSettingsStore);
    }

    [Fact]
    public void ContextualEnumerablePreservesNormalThenContextualRegistrationOrder()
    {
        using var harness = new Harness(services =>
        {
            services.AddSingleton<ISettingsStore, ProjectSettingsStore>();
            services.AddContextualScoped<ISettingsStore, PreviewSettingsStore>("preview");
            services.AddSingleton<ISettingsStore, UserSettingsStore>();
            services.AddContextualScoped<ISettingsStore, LevelSettingsStore>("level:*");
        });

        var stores = harness.Resolve<IEnumerable<ISettingsStore>>("preview", "level:forest").ToArray();

        Assert.Collection(
            stores,
            store => Assert.IsType<ProjectSettingsStore>(store),
            store => Assert.IsType<UserSettingsStore>(store),
            store => Assert.IsType<PreviewSettingsStore>(store),
            store => Assert.IsType<LevelSettingsStore>(store));
    }

    [Fact]
    public void NegativeExactScopedRuleSharesAcrossAllContextsWithoutGroup()
    {
        using var harness = new Harness(services =>
            services.AddContextualScoped<IAssetLoader, RuntimeAssetLoader>("!preview"));

        var first = harness.Resolve<IAssetLoader>("runtime", "level:forest");
        var second = harness.Resolve<IAssetLoader>("runtime", "level:desert");

        Assert.Same(first, second);
    }

    [Fact]
    public void NegativeWildcardScopedRuleSharesAcrossAllContextsWithoutPrefix()
    {
        using var harness = new Harness(services =>
            services.AddContextualScoped<ITransactionMode, NoTransactionMode>("!transaction:*"));

        var first = harness.Resolve<ITransactionMode>("preview", "level:forest");
        var second = harness.Resolve<ITransactionMode>("runtime", "level:desert");

        Assert.Same(first, second);
    }

    [Fact]
    public void SingletonCompositeRuleIgnoresMatchedTuple()
    {
        using var harness = new Harness(services =>
            services.AddContextualSingleton<IPlayerProfile, PlayerProfile>("level:*", "player:*"));

        var first = harness.Resolve<IPlayerProfile>("level:forest", "player:1");
        var second = harness.Resolve<IPlayerProfile>("level:desert", "player:2");

        Assert.Same(first, second);
    }

    [Fact]
    public void DuplicateRuleTermsThrowDuringRegistration()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddContextualScoped<IPlayerProfile, PlayerProfile>("level:*", "level:*"));
    }

    [Fact]
    public void CompositeIdentityIgnoresUnmentionedGroups()
    {
        using var harness = new Harness(services =>
            services.AddContextualScoped<IPlayerProfile, PlayerProfile>("level:*", "player:*"));

        var first = harness.Resolve<IPlayerProfile>("preview", "level:forest", "player:1", "team:red");
        var second = harness.Resolve<IPlayerProfile>("runtime", "level:forest", "player:1", "team:blue");

        Assert.Same(first, second);
    }

    [Fact]
    public void DiagnosticsCaptureContextualResolutionAndCacheReuse()
    {
        using var harness = new Harness(services =>
            services.AddContextualScoped<ITransactionStore, TransactionStore>("transaction:*"));

        _ = harness.Resolve<ITransactionStore>("transaction:abc");
        _ = harness.Resolve<ITransactionStore>("runtime", "transaction:abc");

        var snapshot = harness.CreateDiagnosticsSnapshot();

        Assert.Equal(2, snapshot.Traces.Length);
        Assert.Single(snapshot.Instances);
        Assert.Equal(snapshot.Traces[0].Root.InstanceId, snapshot.Traces[1].Root.InstanceId);
        Assert.False(snapshot.Traces[0].Root.CacheHit);
        Assert.True(snapshot.Traces[1].Root.CacheHit);
        Assert.Equal("transaction:*::transaction:abc", snapshot.Traces[0].Root.Partition);
    }

    [Fact]
    public void DiagnosticsCaptureNestedConstructorDependencies()
    {
        using var harness = new Harness(services =>
        {
            services.AddTransient<AssetPresenter>();
            services.AddContextualScoped<IAssetLoader, PreviewAssetLoader>("preview");
        });

        _ = harness.Resolve<AssetPresenter>("preview");

        var trace = Assert.Single(harness.CreateDiagnosticsSnapshot().Traces);

        Assert.Equal(typeof(AssetPresenter), trace.Root.ServiceType);
        var dependency = Assert.Single(trace.Root.Dependencies);
        Assert.Equal(typeof(IAssetLoader), dependency.ServiceType);
        Assert.Equal(typeof(PreviewAssetLoader), dependency.ImplementationType);
        Assert.Equal("contextual", dependency.Source);
    }

    [Fact]
    public void DiagnosticsCaptureEnumerableItems()
    {
        using var harness = new Harness(services =>
        {
            services.AddSingleton<ISettingsStore, ProjectSettingsStore>();
            services.AddContextualScoped<ISettingsStore, PreviewSettingsStore>("preview");
        });

        _ = harness.Resolve<IEnumerable<ISettingsStore>>("preview");

        var trace = Assert.Single(harness.CreateDiagnosticsSnapshot().Traces);

        Assert.Equal("enumerable", trace.Root.Source);
        Assert.Collection(
            trace.Root.Items,
            item => Assert.Equal(typeof(ProjectSettingsStore), item.ImplementationType),
            item => Assert.Equal(typeof(PreviewSettingsStore), item.ImplementationType));
    }

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly ContextualServiceResolver _resolver;

        public Harness(Action<IServiceCollection> configure)
        {
            var services = new ServiceCollection();
            configure(services);
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = false,
                ValidateScopes = true,
            });
            _resolver = new ContextualServiceResolver(_provider, services);
        }

        public T Resolve<T>(params string[] groups)
            where T : notnull
            => (T)(_resolver.GetService(typeof(T), new ContextualResolutionContext(groups))
                ?? throw new InvalidOperationException($"No service for type '{typeof(T)}' has been registered."));

        public T? ResolveOrNull<T>(params string[] groups)
            where T : class
            => (T?)_resolver.GetService(typeof(T), new ContextualResolutionContext(groups));

        public void DisposeContext(params string[] groups)
            => _resolver.DisposeContext(new ContextualResolutionContext(groups));

        public ServiceResolutionDiagnosticsSnapshot CreateDiagnosticsSnapshot()
            => _resolver.CreateDiagnosticsSnapshot();

        public IReadOnlyList<ContextualServiceMatch> GetContextualServices(params string[] groups)
            => _resolver.GetContextualServices(new ContextualResolutionContext(groups));

        public void Dispose()
        {
            _resolver.Dispose();
            _provider.Dispose();
        }
    }

    private interface IAssetLoader;
    private sealed class PreviewAssetLoader : IAssetLoader;
    private sealed class AlternatePreviewAssetLoader : IAssetLoader;
    private sealed class LevelPreviewAssetLoader : IAssetLoader;
    private sealed class RuntimeAssetLoader : IAssetLoader;

    private interface ILevelSession;
    private sealed class LevelSession : ILevelSession;

    private interface ICameraRig;
    private sealed class EditorCameraRig : ICameraRig;

    private interface ITransactionMode;
    private sealed class NoTransactionMode : ITransactionMode;

    private interface ITransactionStore;
    private sealed class TransactionStore : ITransactionStore;

    private interface IPlayerProfile;
    private sealed class PlayerProfile : IPlayerProfile;

    private interface IModeService;
    private sealed class WildcardModeService : IModeService;
    private sealed class ExactPreviewModeService : IModeService;

    private sealed class AssetPresenter
    {
        public AssetPresenter(IAssetLoader loader)
        {
            Loader = loader;
        }

        public IAssetLoader Loader { get; }
    }

    private interface IAssetPresenter
    {
        IAssetLoader Loader { get; }
    }

    private sealed class ContextualAssetPresenter : IAssetPresenter
    {
        public ContextualAssetPresenter(IAssetLoader loader)
        {
            Loader = loader;
        }

        public IAssetLoader Loader { get; }
    }

    private interface ITransactionPresenter
    {
        ITransactionStore Store { get; }
    }

    private sealed class TransactionPresenter : ITransactionPresenter
    {
        public TransactionPresenter(ITransactionStore store)
        {
            Store = store;
        }

        public ITransactionStore Store { get; }
    }

    private interface ISettingsStore;
    private sealed class ProjectSettingsStore : ISettingsStore;
    private sealed class UserSettingsStore : ISettingsStore;
    private sealed class PreviewSettingsStore : ISettingsStore;
    private sealed class LevelSettingsStore : ISettingsStore;

    private sealed class SettingsManager
    {
        public SettingsManager(IEnumerable<ISettingsStore> stores)
        {
            Stores = stores.ToArray();
        }

        public IReadOnlyList<ISettingsStore> Stores { get; }
    }

    private sealed class Dashboard
    {
        public Dashboard(SettingsManager manager)
        {
            Manager = manager;
        }

        public SettingsManager Manager { get; }
    }

    private sealed class SingletonSettingsShell
    {
        public SingletonSettingsShell(ProjectSettingsStore store)
        {
            Store = store;
        }

        public ProjectSettingsStore Store { get; }
    }

    private sealed class SingletonAssetShell
    {
        public SingletonAssetShell(IAssetLoader loader)
        {
            Loader = loader;
        }

        public IAssetLoader Loader { get; }
    }

    private sealed class DisposableShell : IDisposable
    {
        public DisposableShell(IAssetLoader loader)
        {
            Loader = loader;
        }

        public static int DisposeCount { get; set; }

        public IAssetLoader Loader { get; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private interface IDisposableService;

    private sealed class DisposableService : IDisposableService, IDisposable
    {
        public static int DisposeCount { get; set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
