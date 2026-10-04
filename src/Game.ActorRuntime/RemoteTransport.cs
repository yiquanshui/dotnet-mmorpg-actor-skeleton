using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Net.Sockets;
using Game.Protocol;

namespace Game.ActorRuntime;

public sealed class RemoteTransport
{
    public static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
        .WithSecurity(MessagePackSecurity.UntrustedData);

    public async Task SendAsync(string host, int port, IGameMessage message, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, ct);
        await using var stream = client.GetStream();

        var buffer = new ArrayBufferWriter<byte>(256);
        MessagePackSerializer.Serialize<IGameMessage>(buffer, message, Options);

        var frame = new byte[4 + buffer.WrittenCount];
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(0, 4), buffer.WrittenCount);
        buffer.WrittenSpan.CopyTo(frame.AsSpan(4));

        await stream.WriteAsync(frame, ct);
    }
}
