using Game.ActorRuntime;
using Game.Protocol;

namespace Game.Server;

public sealed class PlayerActor : Actor
{
    private readonly long _playerId;
    private readonly Func<IGameMessage, Task> _send;
    private readonly IActorRef _scene;

    public PlayerActor(long playerId, Func<IGameMessage, Task> send, IActorRef scene) : base($"player:{playerId}")
    {
        _playerId = playerId;
        _send = send;
        _scene = scene;
    }

    public override Task ReceiveAsync(object message)
    {
        switch (message)
        {
            case LoginReq:
                _scene.Tell(new SceneJoin(_playerId, this));
                break;
            case MoveReq move:
                _scene.Tell(new SceneMove(_playerId, move.X, move.Y));
                break;
            case EnterNotify enter:
                _ = _send(enter);
                break;
            case LeaveNotify leave:
                _ = _send(leave);
                break;
            case MoveNotify mv:
                _ = _send(mv);
                break;
        }
        return Task.CompletedTask;
    }
}
