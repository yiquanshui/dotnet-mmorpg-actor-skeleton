using Game.ActorRuntime;
using Game.Protocol;
using System.Net;
using System.Net.Sockets;
using System.IO.Pipelines;

namespace Game.Server;

public sealed class GatewayServer
{
    private readonly ActorSystem _system;
    private readonly int _port;
    private readonly IActorRef _scene;

    public GatewayServer(ActorSystem system, int port, IActorRef scene)
    {
        _system = system;
        _port = port;
        _scene = scene;
    }

    public async Task RunAsync()
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Any, _port));
        listener.Listen(256);

        while (true)
        {
            var socket = await listener.AcceptAsync();
            _ = HandleAsync(socket);
        }
    }

    private async Task HandleAsync(Socket socket)
    {
        long playerId = 0;
        Func<IGameMessage, Task> send = async msg =>
        {
            var pipe = new Pipe();
            Codec.Write(pipe.Writer, msg);
            await pipe.Writer.FlushAsync();
            var result = await pipe.Reader.ReadAsync();
            foreach (var seg in result.Buffer)
                await socket.SendAsync(seg, SocketFlags.None);
            pipe.Reader.AdvanceTo(result.Buffer.End);
        };

        var conn = new Connection();
        await conn.RunAsync(socket, async msg =>
        {
            switch (msg)
            {
                case LoginReq login:
                    playerId = login.PlayerId;
                    var player = _system.GetOrCreate($"player:{playerId}", id => new PlayerActor(playerId, send, _scene));
                    player.Tell(login);
                    await send(new LoginAck { Ok = true });
                    break;
                default:
                    if (playerId != 0)
                        _system.Get($"player:{playerId}")?.Tell(msg);
                    break;
            }
            await ValueTask.CompletedTask;
        });
    }
}
