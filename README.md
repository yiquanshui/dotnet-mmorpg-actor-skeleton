# .NET MMORPG Actor Skeleton

Custom lightweight actor runtime + MessagePack protocol + MMO scene AOI + supervisor restart + remote actor messaging + bot load client.

## Projects

- `src/Game.Protocol` - Shared MessagePack contracts
- `src/Game.Shared` - Shared constants/config POCOs
- `src/Game.ActorRuntime` - Actor runtime, mailbox, supervisor, remote transport
- `src/Game.Server` - TCP gateway, player/scene actors, AOI 9-grid
- `src/Game.Bot` - Load test bots

## Run

```bash
dotnet build
dotnet run --project src/Game.Server
dotnet run --project src/Game.Bot -- --count 200 --duration 60
```
