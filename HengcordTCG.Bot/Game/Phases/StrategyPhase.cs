using HengcordTCG.Shared.Models;

namespace HengcordTCG.Bot.Game.Phases;

public static class StrategyPhase
{
    private const int SafeDrawLimit = 6;

    public static (bool Success, string Message) PlayCard(GameSession session, PlayerState player, int handIndex, bool isCloser = false)
    {
        if (session.CurrentPhase != TurnPhase.Strategy)
            return (false, "Not in Strategy phase");

        if (session.CurrentStrategyPlayer != player)
            return (false, "Not your turn to play");

        Card card;
        if (isCloser)
        {
            if (handIndex < 0 || handIndex >= player.CloserDeck.Count)
                return (false, "Invalid closer card index");
            card = player.CloserDeck[handIndex];
        }
        else
        {
            if (handIndex < 0 || handIndex >= player.Hand.Count)
                return (false, "Invalid card index");
            card = player.Hand[handIndex];
        }

        bool isHeavy = player.DrawsThisTurn > SafeDrawLimit;
        int cost = card.LightCost + (isHeavy ? 1 : 0);

        if (player.Light < cost)
            return (false, $"Not enough Light! Need {cost}, have {player.Light}");

        player.Light -= cost;

        if (isCloser)
            player.CloserDeck.RemoveAt(handIndex);
        else
            player.Hand.RemoveAt(handIndex);

        var unit = new UnitState(card, isHeavy);
        player.WaitingRoom.Add(unit);

        player.HasPassed = false;
        session.IsPlayer1Turn = !session.IsPlayer1Turn;

        var heavyTag = isHeavy ? " [HEAVY]" : "";
        session.GameLog.Add($"  {player.Username} plays {card.Name} (💡{cost}){heavyTag}");

        return (true, $"Played {card.Name}");
    }

    public static void Pass(GameSession session, PlayerState player)
    {
        if (session.CurrentPhase != TurnPhase.Strategy) return;

        player.HasPassed = true;
        session.GameLog.Add($"  {player.Username} passes");

        var opponent = session.GetOpponent(player);
        if (opponent.HasPassed)
        {
            session.CurrentPhase = TurnPhase.Declaration;
            session.GameLog.Add("  → Moving to Declaration phase");
        }
        else
        {
            session.IsPlayer1Turn = !session.IsPlayer1Turn;
        }
    }
}
