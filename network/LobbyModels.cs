using System.Collections.Generic;

// JSON shapes of the lobby HTTP API (project-w-server/internal/lobby).

public sealed class MatchAssignment
{
    public string AI { get; set; }
    public string MatchId { get; set; }
    public string Host { get; set; }
    public int Port { get; set; }
    public string Ticket { get; set; }
    public uint Team { get; set; }
    public string Role { get; set; }
    public uint Hero { get; set; }
    public int Players { get; set; }
    public int Bots { get; set; }
    public bool IsValid => !string.IsNullOrWhiteSpace(MatchId) && !string.IsNullOrWhiteSpace(Host) &&
        Port is > 0 and <= 65535 && Ticket?.Length == 32 && Players == 8 &&
        (Role == "SPECTATOR" && Team == 0 && Hero == 0 && Bots >= 0 && Bots <= Players ||
         Team is 1 or 2 && Bots >= 0 && Bots < Players &&
         (Role == "COMMANDER" && Hero == 0 || Role == "HERO" && UnitCatalog.IsHero(Hero)));
}

public sealed class RegionInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Ping { get; set; } // host:port of the region's ping echo; empty when it has none.
}

public sealed class LobbySession
{
    public string Id { get; set; }
    public string Token { get; set; }
    public string Name { get; set; }
    public List<RegionInfo> Regions { get; set; } = new();
}

public sealed class LobbyMe
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Room { get; set; }
    public MatchAssignment Match { get; set; }
}

public sealed class RoomSummary
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Host { get; set; }
    public int Players { get; set; }
    public int Max { get; set; }
    public string Region { get; set; }
    public string RegionName { get; set; }
    public bool Playing { get; set; }
}

public sealed class RoomMember
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Seat { get; set; }
    public bool Spectator { get; set; }
    public int Ping { get; set; } // To the room's region in milliseconds; -1 before it is measured.
}

public sealed class RoomSeat
{
    public int Index { get; set; }
    public uint Team { get; set; }
    public string Role { get; set; }
    public uint Hero { get; set; } // The guest's pick; 0 for an empty hero seat.
    public string PlayerId { get; set; }
}

public sealed class ChatLine
{
    public int Seq { get; set; }
    public string PlayerId { get; set; }
    public string Name { get; set; }
    public string Text { get; set; }
    public bool System { get; set; }
}

public sealed class RoomState
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string HostId { get; set; }
    public string Region { get; set; }
    public bool RegionAuto { get; set; }
    public List<RegionInfo> Regions { get; set; } = new();
    public string State { get; set; }
    public int Countdown { get; set; }
    public List<RoomMember> Members { get; set; } = new();
    public List<RoomSeat> Seats { get; set; } = new();
    public List<ChatLine> Chat { get; set; } = new();
    public string You { get; set; }
    public MatchAssignment Match { get; set; }

    public bool IsHost => HostId == You;
    public RoomMember Me => Members.Find(m => m.Id == You);
    public RoomMember Member(string id) => Members.Find(m => m.Id == id);
}

public sealed class LobbyError
{
    public string Code { get; set; }
    public string Message { get; set; }
}
