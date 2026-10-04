using System.Collections.Concurrent;

namespace Game.ActorRuntime;

public sealed class ActorSystem
{
    private readonly ConcurrentDictionary<string, Actor> _actors = new();
    public Supervisor Supervisor { get; } = new();

    public T GetOrCreate<T>(string id, Func<string, T> factory) where T : Actor
    {
        return (T)_actors.GetOrAdd(id, key =>
        {
            var actor = factory(key);
            actor.Start(this);
            return actor;
        });
    }

    public IActorRef? Get(string id) => _actors.TryGetValue(id, out var actor) ? actor : null;

    public async Task RestartAsync(Actor oldActor)
    {
        if (_actors.TryRemove(oldActor.Id, out _))
            await oldActor.StopAsync();
    }
}
