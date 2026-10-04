using Game.ActorRuntime;
using Game.Protocol;
using Game.Shared;

namespace Game.Server;

public sealed record SceneJoin(long PlayerId, IActorRef PlayerRef);
public sealed record SceneLeave(long PlayerId);
public sealed record SceneMove(long PlayerId, float X, float Y);

public sealed class SceneActor : Actor
{
    private readonly Dictionary<long, IActorRef> _playerRefs = new();
    private readonly Dictionary<long, (float x, float y, int cx, int cy)> _players = new();
    private readonly Dictionary<(int cx, int cy), HashSet<long>> _cells = new();

    public SceneActor(string id) : base(id) {}

    public override Task ReceiveAsync(object message)
    {
        switch (message)
        {
            case SceneJoin join:
                _playerRefs[join.PlayerId] = join.PlayerRef;
                var (cx, cy) = CellOf(0, 0);
                _players[join.PlayerId] = (0, 0, cx, cy);
                AddToCell(join.PlayerId, cx, cy);
                BroadcastAround(cx, cy, new EnterNotify { PlayerId = join.PlayerId, X = 0, Y = 0 }, except: join.PlayerId);
                break;
            case SceneLeave leave:
                if (_players.TryGetValue(leave.PlayerId, out var p))
                {
                    RemoveFromCell(leave.PlayerId, p.cx, p.cy);
                    _players.Remove(leave.PlayerId);
                    _playerRefs.Remove(leave.PlayerId);
                    BroadcastAround(p.cx, p.cy, new LeaveNotify { PlayerId = leave.PlayerId }, except: leave.PlayerId);
                }
                break;
            case SceneMove move:
                if (!_players.TryGetValue(move.PlayerId, out var old)) break;
                var (ncx, ncy) = CellOf(move.X, move.Y);
                _players[move.PlayerId] = (move.X, move.Y, ncx, ncy);

                if (old.cx != ncx || old.cy != ncy)
                {
                    RemoveFromCell(move.PlayerId, old.cx, old.cy);
                    AddToCell(move.PlayerId, ncx, ncy);

                    var oldSet = NeighborCells(old.cx, old.cy);
                    var newSet = NeighborCells(ncx, ncy);

                    var left = oldSet.Except(newSet).ToArray();
                    var entered = newSet.Except(oldSet).ToArray();

                    foreach (var c in left) BroadcastCell(c, new LeaveNotify { PlayerId = move.PlayerId }, except: move.PlayerId);
                    foreach (var c in entered) BroadcastCell(c, new EnterNotify { PlayerId = move.PlayerId, X = move.X, Y = move.Y }, except: move.PlayerId);
                }

                BroadcastAround(ncx, ncy, new MoveNotify { PlayerId = move.PlayerId, X = move.X, Y = move.Y }, except: move.PlayerId);
                break;
        }

        return Task.CompletedTask;
    }

    private static (int, int) CellOf(float x, float y)
        => ((int)MathF.Floor(Math.Clamp(x, 0, GameConst.SceneWidth - 1) / GameConst.SceneCellSize),
            (int)MathF.Floor(Math.Clamp(y, 0, GameConst.SceneHeight - 1) / GameConst.SceneCellSize));

    private void AddToCell(long playerId, int cx, int cy)
    {
        if (!_cells.TryGetValue((cx, cy), out var hs))
            _cells[(cx, cy)] = hs = new HashSet<long>();
        hs.Add(playerId);
    }

    private void RemoveFromCell(long playerId, int cx, int cy)
    {
        if (_cells.TryGetValue((cx, cy), out var hs))
        {
            hs.Remove(playerId);
            if (hs.Count == 0) _cells.Remove((cx, cy));
        }
    }

    private IEnumerable<(int, int)> NeighborCells(int cx, int cy)
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
            yield return (cx + dx, cy + dy);
    }

    private void BroadcastAround(int cx, int cy, IGameMessage msg, long except)
    {
        foreach (var c in NeighborCells(cx, cy)) BroadcastCell(c, msg, except);
    }

    private void BroadcastCell((int cx, int cy) c, IGameMessage msg, long except)
    {
        if (!_cells.TryGetValue(c, out var hs)) return;
        foreach (var id in hs)
        {
            if (id == except) continue;
            if (_playerRefs.TryGetValue(id, out var aref)) aref.Tell(msg);
        }
    }
}
