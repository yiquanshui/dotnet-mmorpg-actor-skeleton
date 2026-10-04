using System.Collections.Concurrent;

namespace Game.ActorRuntime;

public sealed class Supervisor
{
    private readonly ActorSystem _system;
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _restartWindows = new();

    public Supervisor(ActorSystem system)
    {
        _system = system;
    }

    public async Task<bool> HandleAsync(Actor actor, object message, Exception ex)
    {
        if (ex is OutOfMemoryException)
        {
            Console.WriteLine($"[Supervisor] fatal OOM actor={actor.Id}. stop.");
            return false;
        }

        var now = DateTime.UtcNow;
        var queue = _restartWindows.GetOrAdd(actor.Id, _ => new Queue<DateTime>());
        lock (queue)
        {
            queue.Enqueue(now);
            while (queue.Count > 0 && (now - queue.Peek()).TotalSeconds > 60)
                queue.Dequeue();

            if (queue.Count > 10)
            {
                Console.WriteLine($"[Supervisor] actor={actor.Id} restart storm (>10/60s). stop.");
                _restartWindows.TryRemove(actor.Id, out _);
                return false;
            }
        }

        var delay = ex switch
        {
            TimeoutException => TimeSpan.FromMilliseconds(300),
            InvalidOperationException => TimeSpan.FromMilliseconds(800),
            _ => TimeSpan.FromSeconds(1)
        };

        Console.WriteLine($"[Supervisor] actor={actor.Id} restarting after {delay.TotalMilliseconds}ms due to {ex.GetType().Name}: {ex.Message}");
        await Task.Delay(delay);
        await _system.RestartAsync(actor);
        return true;
    }
}
