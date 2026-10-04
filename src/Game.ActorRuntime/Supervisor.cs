namespace Game.ActorRuntime;

public sealed class Supervisor
{
    public async Task<bool> HandleAsync(Actor actor, object message, Exception ex)
    {
        Console.WriteLine($"[Supervisor] actor={actor.Id} crashed on {message.GetType().Name}, ex={ex.Message}");
        if (ex is OutOfMemoryException)
            return false;

        await Task.Delay(1000);
        await actor.OnStopAsync();
        return true;
    }
}
