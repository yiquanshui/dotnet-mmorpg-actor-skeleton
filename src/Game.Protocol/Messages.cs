using MessagePack;

namespace Game.Protocol;

[MessagePackObject]
[Union(1, typeof(LoginReq))]
[Union(2, typeof(LoginAck))]
[Union(3, typeof(MoveReq))]
[Union(4, typeof(MoveNotify))]
[Union(5, typeof(EnterNotify))]
[Union(6, typeof(LeaveNotify))]
[Union(7, typeof(Ping))]
public interface IGameMessage {}

[MessagePackObject]
public sealed class LoginReq : IGameMessage
{
    [Key(0)] public long PlayerId { get; set; }
}

[MessagePackObject]
public sealed class LoginAck : IGameMessage
{
    [Key(0)] public bool Ok { get; set; }
}

[MessagePackObject]
public sealed class MoveReq : IGameMessage
{
    [Key(0)] public float X { get; set; }
    [Key(1)] public float Y { get; set; }
    [Key(2)] public uint Seq { get; set; }
}

[MessagePackObject]
public sealed class MoveNotify : IGameMessage
{
    [Key(0)] public long PlayerId { get; set; }
    [Key(1)] public float X { get; set; }
    [Key(2)] public float Y { get; set; }
}

[MessagePackObject]
public sealed class EnterNotify : IGameMessage
{
    [Key(0)] public long PlayerId { get; set; }
    [Key(1)] public float X { get; set; }
    [Key(2)] public float Y { get; set; }
}

[MessagePackObject]
public sealed class LeaveNotify : IGameMessage
{
    [Key(0)] public long PlayerId { get; set; }
}

[MessagePackObject]
public sealed class Ping : IGameMessage
{
    [Key(0)] public long Ts { get; set; }
}
