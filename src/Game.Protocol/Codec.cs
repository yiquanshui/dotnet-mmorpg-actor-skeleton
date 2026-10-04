using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using MessagePack;

namespace Game.Protocol;

public static class Codec
{
    private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
        .WithSecurity(MessagePackSecurity.UntrustedData);

    public static void Write(PipeWriter writer, IGameMessage msg)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        MessagePackSerializer.Serialize<IGameMessage>(buffer, msg, Options);

        var total = 4 + buffer.WrittenCount;
        var span = writer.GetSpan(total);
        BinaryPrimitives.WriteInt32BigEndian(span, buffer.WrittenCount);
        buffer.WrittenSpan.CopyTo(span[4..]);
        writer.Advance(total);
    }

    public static bool TryRead(ref ReadOnlySequence<byte> source, out IGameMessage? msg)
    {
        msg = null;
        if (source.Length < 4) return false;

        Span<byte> header = stackalloc byte[4];
        source.Slice(0, 4).CopyTo(header);
        var len = BinaryPrimitives.ReadInt32BigEndian(header);
        if (len <= 0 || len > 256 * 1024) throw new InvalidDataException($"Invalid length={len}");

        if (source.Length < 4 + len) return false;
        var payload = source.Slice(4, len);
        msg = MessagePackSerializer.Deserialize<IGameMessage>(payload, Options);
        source = source.Slice(4 + len);
        return true;
    }
}
