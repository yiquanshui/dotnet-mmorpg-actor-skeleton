using Game.ActorRuntime;
using Game.Server;

var system = new ActorSystem();
var scene = system.GetOrCreate("scene:1", id => new SceneActor(id));
var gateway = new GatewayServer(system, 5000, scene);

Console.WriteLine("Server started at :5000");
await gateway.RunAsync();
