using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Fxyoge.DependencyInjection.Configuration;

public sealed class SettingsTransaction : ISettingsTransaction
{
    private readonly IConfigOverlayStore _store;
    private readonly object _sync = new();
    private readonly List<ITransactionalSettingsParticipant> _participants = new();

    public SettingsTransaction(IConfigOverlayStore store)
    {
        _store = store;
    }

    public bool HasChanges
    {
        get
        {
            lock (_sync)
            {
                return _participants.Any(participant => participant.HasChanges);
            }
        }
    }

    public async ValueTask Save()
    {
        ITransactionalSettingsParticipant[] participants;

        lock (_sync)
        {
            participants = _participants.ToArray();
        }

        foreach (var participant in participants)
        {
            await participant.SaveToLive();
        }

        _store.Save();
    }

    public void Abandon()
    {
        ITransactionalSettingsParticipant[] participants;

        lock (_sync)
        {
            participants = _participants.ToArray();
        }

        foreach (var participant in participants)
        {
            participant.Abandon();
        }
    }

    internal void Enlist(ITransactionalSettingsParticipant participant)
    {
        lock (_sync)
        {
            if (!_participants.Contains(participant))
            {
                _participants.Add(participant);
            }
        }
    }
}

internal interface ITransactionalSettingsParticipant
{
    bool HasChanges { get; }

    ValueTask SaveToLive();

    void Abandon();
}
