using HengcordTCG.Shared.Models;

namespace HengcordTCG.Bot.Game.Phases;

public static class PreparationPhase
{
    private const int InitialDrawCount = 4;

    public static void StartTurn(GameSession session)
    {
        session.TurnNumber++;
        session.CurrentPhase = TurnPhase.Preparation;
        session.LastActivity = DateTime.UtcNow;

        session.Player1.DrawsThisTurn = 0;
        session.Player1.HasPassed = false;
        session.Player2.DrawsThisTurn = 0;
        session.Player2.HasPassed = false;
        session.LastCombatResults.Clear();

        foreach (var lane in session.Lanes)
        {
            lane.Attacker = null;
            lane.Defender = null;
        }

        GameEngine.DrawCards(session.Player1, InitialDrawCount, session.GameLog);
        GameEngine.DrawCards(session.Player2, InitialDrawCount, session.GameLog);

        session.CurrentPhase = TurnPhase.Strategy;
        session.IsPlayer1Turn = session.IsPlayer1Attacker;

        session.GameLog.Add($"── Turn {session.TurnNumber} ── {session.Attacker.Username} attacks, {session.Defender.Username} defends");
    }
}
