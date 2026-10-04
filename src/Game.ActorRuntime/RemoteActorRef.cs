using Game.Protocol;

namespace Game.ActorRuntime;

public sealed class RemoteActorRef : IActorRef
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _actorId;
    private readonly RemoteTransport _transport = new();

    public RemoteActorRef(string host, int port, string actorId)
    {
        _host = host;
        _port = port;
        _actorId = actorId;
    }

    public string ActorId => _actorId;

    public bool Tell(object message)
    {
        if (message is not IGameMessage gameMessage)
            return false;

        _ = _transport.SendAsync(_host, _port, gameMessage);
        return true;
    }
}
