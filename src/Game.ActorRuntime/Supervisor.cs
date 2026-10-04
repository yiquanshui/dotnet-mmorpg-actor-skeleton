using System.Collections.Concurrent;

namespace Game.ActorRuntime;

public sealed class Supervisor
{
    private readonly ActorSystem _system;
    private readonly ConcurrentDictionary<string, int> _restartCounts = new();

    public Supervisor(ActorSystem system)
    {
        _system = system;
    }

    public async Task<bool> HandleAsync(Actor actor, object message, Exception ex)
    {
        var count = _restartCounts.AddOrUpdate(actor.Id, 1, (_, old) => old + 1);
        Console.WriteLine($"[Supervisor] actor={actor.Id}, message={message.GetType().Name}, ex={ex.Message}, restartCount={count}");

        if (count > 5)
        {
            _restartCounts.TryRemove(actor.Id, out _);
            Console.WriteLine($"[Supervisor] actor={actor.Id} exceeded restart limit. stopping.");
            return false;
        }

        await Task.Delay(TimeSpan.FromSeconds(Math.Min(2 * count, 10)));
        await _system.RestartAsync(actor);
        _restartCounts.TryRemove(actor.Id, out _);
        return true;
    }
}
