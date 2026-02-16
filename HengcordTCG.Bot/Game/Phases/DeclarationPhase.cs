using HengcordTCG.Shared.Models;

namespace HengcordTCG.Bot.Game.Phases;

public static class DeclarationPhase
{
    private const int LaneCount = 3;

    public static (bool Success, string Message) AssignAttacker(GameSession session, PlayerState player, int waitingRoomIndex, int laneIndex)
    {
        if (session.CurrentPhase != TurnPhase.Declaration)
            return (false, "Not in Declaration phase");

        if (player != session.Attacker)
            return (false, "Only the attacker can assign to lanes");

        if (laneIndex < 0 || laneIndex >= LaneCount)
            return (false, "Invalid lane (0-2)");

        if (session.Lanes[laneIndex].Attacker != null)
            return (false, $"Lane {laneIndex + 1} already has an attacker");

        if (waitingRoomIndex < 0 || waitingRoomIndex >= player.WaitingRoom.Count)
            return (false, "Invalid unit index");

        var unit = player.WaitingRoom[waitingRoomIndex];
        player.WaitingRoom.RemoveAt(waitingRoomIndex);
        session.Lanes[laneIndex].Attacker = unit;

        session.GameLog.Add($"  {player.Username} → Lane {laneIndex + 1}: {unit.Card.Name}");
        return (true, $"Assigned {unit.Card.Name} to Lane {laneIndex + 1}");
    }

    public static (bool Success, string Message) AssignDefender(GameSession session, PlayerState player, int waitingRoomIndex, int laneIndex)
    {
        if (session.CurrentPhase != TurnPhase.Declaration)
            return (false, "Not in Declaration phase");

        if (player != session.Defender)
            return (false, "Only the defender can block lanes");

        if (laneIndex < 0 || laneIndex >= LaneCount)
            return (false, "Invalid lane (0-2)");

        if (session.Lanes[laneIndex].Attacker == null)
            return (false, $"Cannot block Lane {laneIndex + 1} — no attacker there");

        if (session.Lanes[laneIndex].Defender != null)
            return (false, $"Lane {laneIndex + 1} already has a defender");

        if (waitingRoomIndex < 0 || waitingRoomIndex >= player.WaitingRoom.Count)
            return (false, "Invalid unit index");

        var unit = player.WaitingRoom[waitingRoomIndex];
        player.WaitingRoom.RemoveAt(waitingRoomIndex);
        session.Lanes[laneIndex].Defender = unit;

        session.GameLog.Add($"  {player.Username} blocks Lane {laneIndex + 1}: {unit.Card.Name}");
        return (true, $"Blocked Lane {laneIndex + 1} with {unit.Card.Name}");
    }

    public static void FinishDeclaration(GameSession session)
    {
        session.CurrentPhase = TurnPhase.Combat;
        session.GameLog.Add("  → Moving to Combat phase");
    }
}
