using System.Threading.Channels;

namespace Game.ActorRuntime;

public interface IActorRef
{
    bool Tell(object message);
}

public abstract class Actor : IActorRef
{
    private readonly Channel<object> _mailbox = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly CancellationTokenSource _cts = new();
    private Task _loop = Task.CompletedTask;

    public string Id { get; }
    protected ActorSystem System { get; private set; } = null!;

    protected Actor(string id) => Id = id;

    internal void Start(ActorSystem system)
    {
        System = system;
        _loop = Task.Run(RunAsync);
    }

    public bool Tell(object message) => _mailbox.Writer.TryWrite(message);

    private async Task RunAsync()
    {
        await OnStartAsync();
        try
        {
            var reader = _mailbox.Reader;
            while (await reader.WaitToReadAsync(_cts.Token))
            {
                while (reader.TryRead(out var msg))
                {
                    try
                    {
                        await ReceiveAsync(msg);
                    }
                    catch (Exception ex)
                    {
                        if (!await System.Supervisor.HandleAsync(this, msg, ex))
                            return;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await OnStopAsync();
        }
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        _mailbox.Writer.TryComplete();
        await _loop;
    }

    public virtual Task OnStartAsync() => Task.CompletedTask;
    public virtual Task OnStopAsync() => Task.CompletedTask;
    public abstract Task ReceiveAsync(object message);
}
