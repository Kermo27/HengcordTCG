using HengcordTCG.Shared.Models;

namespace HengcordTCG.Bot.Game.Phases;

public static class ResolutionPhase
{
    public static void ResolveEndOfTurn(GameSession session)
    {
        if (session.Player1.CommanderHp <= 0)
        {
            session.IsFinished = true;
            session.Winner = session.Player2;
            session.GameLog.Add($"  🏆 {session.Player2.Username} wins! {session.Player1.Username}'s Commander is defeated!");
            return;
        }
        if (session.Player2.CommanderHp <= 0)
        {
            session.IsFinished = true;
            session.Winner = session.Player1;
            session.GameLog.Add($"  🏆 {session.Player1.Username} wins! {session.Player2.Username}'s Commander is defeated!");
            return;
        }

        foreach (var lane in session.Lanes)
        {
            if (lane.Attacker is { CurrentHp: > 0 })
                session.Attacker.WaitingRoom.Add(lane.Attacker);
            if (lane.Defender is { CurrentHp: > 0 })
                session.Defender.WaitingRoom.Add(lane.Defender);
        }

        BleedoutUnits(session.Player1);
        BleedoutUnits(session.Player2);

        CleanSlate(session.Player1, session.GameLog);
        CleanSlate(session.Player2, session.GameLog);

        session.IsPlayer1Attacker = !session.IsPlayer1Attacker;

        session.GameLog.Add($"  → Roles swapped: {session.Attacker.Username} is now the Attacker");
    }

    private static void BleedoutUnits(PlayerState player)
    {
        for (int i = player.WaitingRoom.Count - 1; i >= 0; i--)
        {
            var unit = player.WaitingRoom[i];
            if (unit.CurrentHp < unit.Card.Health)
            {
                unit.CurrentHp--;
                if (unit.CurrentHp <= 0)
                {
                    player.WaitingRoom.RemoveAt(i);
                    player.DiscardPile.Add(unit.Card);
                }
            }
        }
    }

    private static void CleanSlate(PlayerState player, List<string> log)
    {
        int lightGained = 1;

        foreach (var card in player.Hand)
        {
            player.DiscardPile.Add(card);
            lightGained++;
        }
        player.Hand.Clear();

        player.Light = Math.Min(player.Light + lightGained, player.MaxLight);
        log.Add($"  {player.Username}: +{lightGained} Light → {player.Light}/{player.MaxLight}");
    }
}
