using System.Collections.Concurrent;
using System.Diagnostics;
using Game.Protocol;

namespace Game.Bot;

public sealed class BotStats
{
    public long Sent { get; private set; }
    public long Received { get; private set; }
    public long TotalLatencyTicks { get; private set; }

    public void RecordSend() => Interlocked.Increment(ref Sent);
    public void RecordReceive() => Interlocked.Increment(ref Received);
    public void RecordLatency(long ticks) => Interlocked.Add(ref TotalLatencyTicks, ticks);
}

public static class BotRunner
{
    public static async Task RunAsync(int count, int durationSeconds)
    {
        var stats = new BotStats();
        var tasks = Enumerable.Range(1, count)
            .Select(id => RunSingleBotAsync(id, durationSeconds, stats));

        await Task.WhenAll(tasks);

        Console.WriteLine($"Total sent={stats.Sent}, received={stats.Received}, avg latency={TimeSpan.FromTicks(stats.TotalLatencyTicks / Math.Max(stats.Sent, 1))}");
    }

    private static async Task RunSingleBotAsync(int id, int durationSeconds, BotStats stats)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", 5000);
        using var stream = client.GetStream();

        await SendAsync(stream, new LoginReq { PlayerId = id });

        var rnd = new Random(id * 7919);
        var end = DateTime.UtcNow.AddSeconds(durationSeconds);
        uint seq = 1;

        while (DateTime.UtcNow < end)
        {
            var move = new MoveReq
            {
                X = (float)(rnd.NextDouble() * 500),
                Y = (float)(rnd.NextDouble() * 500),
                Seq = seq++
            };

            var timer = Stopwatch.StartNew();
            await SendAsync(stream, move);
            timer.Stop();

            stats.RecordSend();
            stats.RecordLatency(timer.ElapsedTicks);
            await Task.Delay(50);
        }
    }

    private static async Task SendAsync(NetworkStream stream, IGameMessage message)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        MessagePackSerializer.Serialize<IGameMessage>(buffer, message, MessagePackSerializerOptions.Standard);

        var frame = new byte[4 + buffer.WrittenCount];
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(0, 4), buffer.WrittenCount);
        buffer.WrittenSpan.CopyTo(frame.AsSpan(4));

        await stream.WriteAsync(frame, 0, frame.Length);
    }
}

public static class Program
{
    public static async Task Main(string[] args)
    {
        var count = 100;
        var duration = 30;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--count" && i + 1 < args.Length)
                count = int.Parse(args[++i]);
            else if (args[i] == "--duration" && i + 1 < args.Length)
                duration = int.Parse(args[++i]);
        }

        Console.WriteLine($"Bot start count={count}, duration={duration}s");
        await BotRunner.RunAsync(count, duration);
    }
}
