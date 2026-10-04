using System.IO.Pipelines;
using System.Net.Sockets;
using Game.Protocol;

var count = 100;
var duration = 30;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--count" && i + 1 < args.Length) count = int.Parse(args[++i]);
    else if (args[i] == "--duration" && i + 1 < args.Length) duration = int.Parse(args[++i]);
}

Console.WriteLine($"Bot start count={count}, duration={duration}s");

var tasks = Enumerable.Range(1, count).Select(id => RunBotAsync(id, duration));
await Task.WhenAll(tasks);
Console.WriteLine("Bot finished.");

static async Task RunBotAsync(int id, int duration)
{
    using var client = new TcpClient();
    await client.ConnectAsync("127.0.0.1", 5000);
    var socket = client.Client;

    await SendAsync(socket, new LoginReq { PlayerId = id });

    var rnd = new Random(id * 9973);
    var end = DateTime.UtcNow.AddSeconds(duration);
    uint seq = 1;

    while (DateTime.UtcNow < end)
    {
        var x = (float)rnd.NextDouble() * 1000;
        var y = (float)rnd.NextDouble() * 1000;
        await SendAsync(socket, new MoveReq { X = x, Y = y, Seq = seq++ });
        await Task.Delay(50);
    }
}

static async Task SendAsync(Socket socket, IGameMessage msg)
{
    var pipe = new Pipe();
    Codec.Write(pipe.Writer, msg);
    await pipe.Writer.FlushAsync();
    var result = await pipe.Reader.ReadAsync();
    foreach (var seg in result.Buffer)
        await socket.SendAsync(seg, SocketFlags.None);
    pipe.Reader.AdvanceTo(result.Buffer.End);
}
