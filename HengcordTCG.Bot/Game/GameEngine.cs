using HengcordTCG.Shared.Models;
using HengcordTCG.Bot.Game.Phases;

namespace HengcordTCG.Bot.Game;

public static class GameEngine
{
    private const int InitialDrawCount = 4;
    private const int ReshuffleCostDraws = 1;

    public static void StartTurn(GameSession session)
    {
        PreparationPhase.StartTurn(session);
    }

    public static void DrawCards(PlayerState player, int count, List<string> log)
    {
        for (int i = 0; i < count; i++)
        {
            if (player.MainDeck.Count == 0)
            {
                if (player.DiscardPile.Count == 0)
                {
                    log.Add($"  {player.Username} has no cards to draw!");
                    return;
                }

                player.MainDeck.AddRange(player.DiscardPile);
                player.DiscardPile.Clear();
                ShuffleList(player.MainDeck);

                player.MaxLight++;
                log.Add($"  {player.Username} reshuffled! Max Light → {player.MaxLight}");

                count -= ReshuffleCostDraws;
                if (i >= count) break;
            }

            var card = player.MainDeck[0];
            player.MainDeck.RemoveAt(0);
            player.Hand.Add(card);
            player.DrawsThisTurn++;
        }
    }

    public static (bool Success, string Message) PlayCard(GameSession session, PlayerState player, int handIndex, bool isCloser = false)
    {
        return StrategyPhase.PlayCard(session, player, handIndex, isCloser);
    }

    public static void Pass(GameSession session, PlayerState player)
    {
        StrategyPhase.Pass(session, player);
    }

    public static (bool Success, string Message) AssignAttacker(GameSession session, PlayerState player, int waitingRoomIndex, int laneIndex)
    {
        return DeclarationPhase.AssignAttacker(session, player, waitingRoomIndex, laneIndex);
    }

    public static (bool Success, string Message) AssignDefender(GameSession session, PlayerState player, int waitingRoomIndex, int laneIndex)
    {
        return DeclarationPhase.AssignDefender(session, player, waitingRoomIndex, laneIndex);
    }

    public static void FinishDeclaration(GameSession session)
    {
        DeclarationPhase.FinishDeclaration(session);
    }

    public static List<ClashResult> ResolveCombat(GameSession session)
    {
        return CombatPhase.ResolveCombat(session);
    }

    public static void ResolveEndOfTurn(GameSession session)
    {
        ResolutionPhase.ResolveEndOfTurn(session);
    }

    internal static int RollDamage(int min, int max)
    {
        if (max <= 0) return 0;
        if (min > max) min = max;
        return Random.Shared.Next(min, max + 1);
    }

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
