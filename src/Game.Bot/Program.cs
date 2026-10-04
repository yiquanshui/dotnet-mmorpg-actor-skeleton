using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using Game.Protocol;
using MessagePack;

namespace Game.Bot;

public sealed class BotStats
{
    private long _sent;
    private long _received;
    private readonly ConcurrentQueue<long> _latencyMs = new();

    public void RecordSend() => Interlocked.Increment(ref _sent);
    public void RecordReceive() => Interlocked.Increment(ref _received);
    public void RecordLatencyMs(long ms) => _latencyMs.Enqueue(ms);

    public long Sent => Interlocked.Read(ref _sent);
    public long Received => Interlocked.Read(ref _received);

    public (double p50, double p95, double p99, double avg) Snapshot()
    {
        var arr = _latencyMs.ToArray();
        if (arr.Length == 0) return (0, 0, 0, 0);
        Array.Sort(arr);
        double avg = arr.Average();
        long P(double p)
        {
            var idx = (int)Math.Ceiling(p * arr.Length) - 1;
            idx = Math.Clamp(idx, 0, arr.Length - 1);
            return arr[idx];
        }
        return (P(0.50), P(0.95), P(0.99), avg);
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

        var stats = new BotStats();
        Console.WriteLine($"Bot start count={count}, duration={duration}s");

        var tasks = Enumerable.Range(1, count)
            .Select(id => RunSingleBotAsync(id, duration, stats));

        await Task.WhenAll(tasks);

        var (p50, p95, p99, avg) = stats.Snapshot();
        Console.WriteLine($"sent={stats.Sent}, recv={stats.Received}, avg={avg:F2}ms, p50={p50:F2}ms, p95={p95:F2}ms, p99={p99:F2}ms");
    }

    private static async Task RunSingleBotAsync(int id, int durationSeconds, BotStats stats)
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", 5000);
        using var stream = client.GetStream();

        await SendAsync(stream, new LoginReq { PlayerId = id });

        var cts = new CancellationTokenSource();
        var recvTask = ReceiveLoop(stream, stats, cts.Token);

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

            var start = DateTime.UtcNow;
            await SendAsync(stream, move);
            var latency = (long)(DateTime.UtcNow - start).TotalMilliseconds;

            stats.RecordSend();
            stats.RecordLatencyMs(latency);
            await Task.Delay(50);
        }

        cts.Cancel();
        await recvTask;
    }

    private static async Task ReceiveLoop(NetworkStream stream, BotStats stats, CancellationToken ct)
    {
        var cache = new List<byte>(4096);
        var temp = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (!stream.DataAvailable)
                {
                    await Task.Delay(5, ct);
                    continue;
                }

                var n = await stream.ReadAsync(temp.AsMemory(0, temp.Length), ct);
                if (n <= 0) break;
                cache.AddRange(temp.AsSpan(0, n).ToArray());

                while (cache.Count >= 4)
                {
                    var len = BinaryPrimitives.ReadInt32BigEndian(CollectionsMarshal.AsSpan(cache)[0..4]);
                    if (len <= 0 || len > 256 * 1024) throw new InvalidDataException($"Bad frame len={len}");
                    if (cache.Count < 4 + len) break;

                    var payload = cache.GetRange(4, len).ToArray();
                    _ = MessagePackSerializer.Deserialize<IGameMessage>(payload);
                    stats.RecordReceive();
                    cache.RemoveRange(0, 4 + len);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task SendAsync(NetworkStream stream, IGameMessage message)
    {
        var payload = MessagePackSerializer.Serialize<IGameMessage>(message);
        var frame = new byte[4 + payload.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(0, 4), payload.Length);
        payload.CopyTo(frame.AsSpan(4));
        await stream.WriteAsync(frame);
    }
}
