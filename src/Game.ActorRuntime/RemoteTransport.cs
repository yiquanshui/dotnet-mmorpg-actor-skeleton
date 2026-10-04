using Game.Protocol;
using System.Net.Sockets;
using System.IO.Pipelines;

namespace Game.ActorRuntime;

public sealed class RemoteTransport
{
    public async Task SendAsync(string host, int port, IGameMessage message, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, ct);
        var stream = client.GetStream();
        var pipe = new Pipe();
        Codec.Write(pipe.Writer, message);
        await pipe.Writer.FlushAsync(ct);
        var read = await pipe.Reader.ReadAsync(ct);
        foreach (var seg in read.Buffer)
            await stream.WriteAsync(seg, ct);
        pipe.Reader.AdvanceTo(read.Buffer.End);
    }
}
