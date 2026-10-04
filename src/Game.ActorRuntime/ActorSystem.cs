using System.Collections.Concurrent;

namespace Game.ActorRuntime;

public sealed class ActorSystem
{
    private readonly ConcurrentDictionary<string, Actor> _actors = new();
    private readonly ConcurrentDictionary<string, Func<string, Actor>> _factories = new();

    public ActorSystem()
    {
        Supervisor = new Supervisor(this);
    }

    public Supervisor Supervisor { get; }

    public T GetOrCreate<T>(string id, Func<string, T> factory) where T : Actor
    {
        return (T)_actors.GetOrAdd(id, key =>
        {
            _factories[key] = k => factory(k);
            var actor = factory(key);
            actor.Start(this);
            return actor;
        });
    }

    public IActorRef? Get(string id) => _actors.TryGetValue(id, out var actor) ? actor : null;

    public async Task RestartAsync(Actor actor)
    {
        if (!_actors.TryRemove(actor.Id, out _))
            return;

        await actor.StopAsync();

        if (_factories.TryGetValue(actor.Id, out var factory))
        {
            var replacement = factory(actor.Id);
            replacement.Start(this);
            _actors[actor.Id] = replacement;
        }
    }
}
