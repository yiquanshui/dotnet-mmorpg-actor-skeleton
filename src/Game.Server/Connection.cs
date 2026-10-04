using System.IO.Pipelines;
using System.Net.Sockets;
using Game.Protocol;

namespace Game.Server;

public sealed class Connection
{
    public async Task RunAsync(Socket socket, Func<IGameMessage, ValueTask> onMessage)
    {
        var pipe = new Pipe();
        var fill = FillAsync(socket, pipe.Writer);
        var read = ReadAsync(pipe.Reader, onMessage);
        await Task.WhenAll(fill, read);
    }

    private static async Task FillAsync(Socket s, PipeWriter w)
    {
        try
        {
            while (true)
            {
                var mem = w.GetMemory(4096);
                int n = await s.ReceiveAsync(mem, SocketFlags.None);
                if (n == 0) break;
                w.Advance(n);
                if ((await w.FlushAsync()).IsCompleted) break;
            }
        }
        finally
        {
            await w.CompleteAsync();
        }
    }

    private static async Task ReadAsync(PipeReader r, Func<IGameMessage, ValueTask> onMessage)
    {
        try
        {
            while (true)
            {
                var result = await r.ReadAsync();
                var buf = result.Buffer;
                while (Codec.TryRead(ref buf, out var msg))
                    await onMessage(msg!);
                r.AdvanceTo(buf.Start, buf.End);
                if (result.IsCompleted) break;
            }
        }
        finally
        {
            await r.CompleteAsync();
        }
    }
}
