using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class SettingsTransaction : ISettingsTransaction
{
    private readonly IConfigOverlayStore _store;
    private readonly object _sync = new();
    private readonly List<SettingsTransactionParticipant> _participants = new();

    public SettingsTransaction(IConfigOverlayStore store)
    {
        _store = store;
    }

    public async ValueTask Save()
    {
        SettingsTransactionParticipant[] participants;

        lock (_sync)
        {
            participants = _participants.ToArray();
        }

        var overlaySnapshot = _store.CreateSnapshot();
        var preparedCommits = new List<IPreparedSettingsCommit>();

        try
        {
            foreach (var participant in participants)
            {
                if (participant.HasChanges())
                {
                    preparedCommits.Add(participant.PrepareSaveToLive());
                }
            }

            if (preparedCommits.Count == 0)
            {
                return;
            }

            _store.Save();
        }
        catch
        {
            _store.RestoreSnapshot(overlaySnapshot);
            throw;
        }

        try
        {
            foreach (var commit in preparedCommits)
            {
                commit.ApplyRuntime();
            }
        }
        catch
        {
            foreach (var commit in preparedCommits.AsEnumerable().Reverse())
            {
                commit.RollbackRuntime();
            }

            _store.RestoreSnapshot(overlaySnapshot);
            _store.Save();
            throw;
        }

        foreach (var commit in preparedCommits)
        {
            commit.Publish();
        }

        await ValueTask.CompletedTask;
    }

    public bool HasChanges
    {
        get
        {
            lock (_sync)
            {
                return _participants.Any(participant => participant.HasChanges());
            }
        }
    }

    public void Abandon()
    {
        SettingsTransactionParticipant[] participants;

        lock (_sync)
        {
            participants = _participants.ToArray();
        }

        foreach (var participant in participants)
        {
            participant.Abandon();
        }
    }

    internal void Enlist(
        object owner,
        Func<bool> hasChanges,
        Func<IPreparedSettingsCommit> prepareSaveToLive,
        Action abandon)
    {
        lock (_sync)
        {
            if (_participants.Any(participant => ReferenceEquals(participant.Owner, owner)))
            {
                return;
            }

            _participants.Add(new SettingsTransactionParticipant(
                owner,
                hasChanges,
                prepareSaveToLive,
                abandon));
        }
    }

    internal void Unenlist(object owner)
    {
        lock (_sync)
        {
            _participants.RemoveAll(participant => ReferenceEquals(participant.Owner, owner));
        }
    }

    private sealed record SettingsTransactionParticipant(
        object Owner,
        Func<bool> HasChanges,
        Func<IPreparedSettingsCommit> PrepareSaveToLive,
        Action Abandon);
}

internal interface IPreparedSettingsCommit
{
    void ApplyRuntime();

    void RollbackRuntime();

    void Publish();
}

internal sealed class PreparedSettingsCommit : IPreparedSettingsCommit
{
    public static PreparedSettingsCommit Empty { get; } = new();

    private PreparedSettingsCommit()
    {
    }

    public void ApplyRuntime()
    {
    }

    public void RollbackRuntime()
    {
    }

    public void Publish()
    {
    }
}
