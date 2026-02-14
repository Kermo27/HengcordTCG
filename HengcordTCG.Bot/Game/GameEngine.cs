using HengcordTCG.Shared.Models;

namespace HengcordTCG.Bot.Game;

/// <summary>
/// Stateless game rules engine. All methods take a GameSession and mutate it.
/// Implements the 5-phase turn cycle from the design document.
/// </summary>
public static class GameEngine
{
    private const int InitialDrawCount = 4;
    private const int SafeDrawLimit = 6;
    private const int ReshuffleCostDraws = 1;
    private const int LaneCount = 3;

    // ──────────────────────────────────────────────
    // PHASE 1: PREPARATION
    // ──────────────────────────────────────────────

    /// <summary>Start a new turn: increment turn, draw cards for both players, trigger start-of-turn effects.</summary>
    public static void StartTurn(GameSession session)
    {
        session.TurnNumber++;
        session.CurrentPhase = TurnPhase.Preparation;
        session.LastActivity = DateTime.UtcNow;

        // Reset per-turn state
        session.Player1.DrawsThisTurn = 0;
        session.Player1.HasPassed = false;
        session.Player2.DrawsThisTurn = 0;
        session.Player2.HasPassed = false;
        session.LastCombatResults.Clear();

        // Clear lanes
        foreach (var lane in session.Lanes)
        {
            lane.Attacker = null;
            lane.Defender = null;
        }

        // Both players draw initial 4 cards
        DrawCards(session.Player1, InitialDrawCount, session.GameLog);
        DrawCards(session.Player2, InitialDrawCount, session.GameLog);

        // Move to Strategy phase
        session.CurrentPhase = TurnPhase.Strategy;

        // Faster player (attacker) acts first
        session.IsPlayer1Turn = session.IsPlayer1Attacker;

        session.GameLog.Add($"── Turn {session.TurnNumber} ── {session.Attacker.Username} attacks, {session.Defender.Username} defends");
    }

    /// <summary>
    /// Draw cards from the main deck into hand.
    /// Applies reshuffle cost (1 draw) and overheat (7th+ card = Heavy).
    /// </summary>
    public static void DrawCards(PlayerState player, int count, List<string> log)
    {
        for (int i = 0; i < count; i++)
        {
            if (player.MainDeck.Count == 0)
            {
                // Reshuffle discard pile into main deck
                if (player.DiscardPile.Count == 0)
                {
                    log.Add($"  {player.Username} has no cards to draw!");
                    return;
                }

                player.MainDeck.AddRange(player.DiscardPile);
                player.DiscardPile.Clear();
                ShuffleList(player.MainDeck);

                // Increase max light on reshuffle
                player.MaxLight++;
                log.Add($"  {player.Username} reshuffled! Max Light → {player.MaxLight}");

                // Reshuffle costs 1 draw
                count -= ReshuffleCostDraws;
                if (i >= count) break;
            }

            var card = player.MainDeck[0];
            player.MainDeck.RemoveAt(0);
            player.Hand.Add(card);
            player.DrawsThisTurn++;
        }
    }

    // ──────────────────────────────────────────────
    // PHASE 2: STRATEGY (Bullet Exchange)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Play a card from hand. Returns true if successful.
    /// Cards drawn beyond the 6th are "Heavy" (+1 cost, no discard bonus).
    /// </summary>
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

        // Calculate cost with Heavy penalty
        bool isHeavy = player.DrawsThisTurn > SafeDrawLimit;
        int cost = card.LightCost + (isHeavy ? 1 : 0);

        if (player.Light < cost)
            return (false, $"Not enough Light! Need {cost}, have {player.Light}");

        // Pay the cost
        player.Light -= cost;

        // Remove from hand/closer deck
        if (isCloser)
            player.CloserDeck.RemoveAt(handIndex);
        else
            player.Hand.RemoveAt(handIndex);

        // Summon unit to Waiting Room
        var unit = new UnitState(card, isHeavy);
        player.WaitingRoom.Add(unit);

        // Reset both players' pass state when a card is played
        player.HasPassed = false;

        // Switch turns
        session.IsPlayer1Turn = !session.IsPlayer1Turn;

        var heavyTag = isHeavy ? " [HEAVY]" : "";
        session.GameLog.Add($"  {player.Username} plays {card.Name} (💡{cost}){heavyTag}");

        return (true, $"Played {card.Name}");
    }

    /// <summary>Player passes. If both pass consecutively, move to Declaration.</summary>
    public static void Pass(GameSession session, PlayerState player)
    {
        if (session.CurrentPhase != TurnPhase.Strategy) return;

        player.HasPassed = true;
        session.GameLog.Add($"  {player.Username} passes");

        var opponent = session.GetOpponent(player);
        if (opponent.HasPassed)
        {
            // Both passed — move to Declaration
            session.CurrentPhase = TurnPhase.Declaration;
            session.GameLog.Add("  → Moving to Declaration phase");
        }
        else
        {
            // Switch turns
            session.IsPlayer1Turn = !session.IsPlayer1Turn;
        }
    }

    // ──────────────────────────────────────────────
    // PHASE 3: DECLARATION
    // ──────────────────────────────────────────────

    /// <summary>Attacker assigns a unit from Waiting Room to a lane.</summary>
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

    /// <summary>Defender assigns a unit to block a lane that has an attacker.</summary>
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

    /// <summary>Finalize declaration and move to Combat phase.</summary>
    public static void FinishDeclaration(GameSession session)
    {
        session.CurrentPhase = TurnPhase.Combat;
        session.GameLog.Add("  → Moving to Combat phase");
    }

    // ──────────────────────────────────────────────
    // PHASE 4: COMBAT
    // ──────────────────────────────────────────────

    /// <summary>Resolve all lanes from left to right. Returns combat results.</summary>
    public static List<ClashResult> ResolveCombat(GameSession session)
    {
        var results = new List<ClashResult>();

        for (int i = 0; i < LaneCount; i++)
        {
            var lane = session.Lanes[i];
            if (lane.Attacker == null) continue;

            var result = new ClashResult
            {
                LaneIndex = i,
                Attacker = lane.Attacker,
                Defender = lane.Defender,
            };

            if (lane.Defender != null)
            {
                // Clash — both roll attack dice
                result.AttackerRoll = RollDie(lane.Attacker.Card.DieSize);
                result.DefenderRoll = RollDie(lane.Defender.Card.DieSize);

                if (result.AttackerRoll >= result.DefenderRoll)
                {
                    // Attacker wins — deals full roll damage to defender
                    result.DamageDealt = result.AttackerRoll;
                    lane.Defender.CurrentHp -= result.DamageDealt;
                    result.AttackerWon = true;
                    result.Description = $"Lane {i + 1}: {lane.Attacker.Card.Name} (🎲{result.AttackerRoll}) beats {lane.Defender.Card.Name} (🎲{result.DefenderRoll}) — {result.DamageDealt} dmg!";
                }
                else
                {
                    // Defender wins
                    result.DamageDealt = result.DefenderRoll;
                    lane.Attacker.CurrentHp -= result.DamageDealt;
                    result.AttackerWon = false;
                    result.Description = $"Lane {i + 1}: {lane.Defender.Card.Name} (🎲{result.DefenderRoll}) beats {lane.Attacker.Card.Name} (🎲{result.AttackerRoll}) — {result.DamageDealt} dmg!";
                }
            }
            else
            {
                // Unopposed — attacker hits Commander directly
                result.WasUnopposed = true;
                result.AttackerRoll = RollDie(lane.Attacker.Card.DieSize);
                result.DamageDealt = result.AttackerRoll;
                session.Defender.CommanderHp -= result.DamageDealt;
                result.AttackerWon = true;

                // Commander counter-strike
                result.CounterStrikeDamage = session.Defender.Commander.CounterStrike;
                lane.Attacker.CurrentHp -= result.CounterStrikeDamage;

                result.Description = $"Lane {i + 1}: {lane.Attacker.Card.Name} (🎲{result.AttackerRoll}) → Commander! {result.DamageDealt} dmg! Counter-strike: {result.CounterStrikeDamage} dmg back!";
            }

            results.Add(result);
        }

        session.LastCombatResults = results;
        session.CurrentPhase = TurnPhase.Resolution;
        session.GameLog.Add("  → Combat resolved");

        return results;
    }

    // ──────────────────────────────────────────────
    // PHASE 5: RESOLUTION
    // ──────────────────────────────────────────────

    /// <summary>
    /// Resolve end-of-turn: check victory, retreat units, bleedout, discard hand, gain light, swap roles.
    /// </summary>
    public static void ResolveEndOfTurn(GameSession session)
    {
        // Check victory
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

        // Retreat — all surviving board units return to Waiting Room
        foreach (var lane in session.Lanes)
        {
            if (lane.Attacker is { CurrentHp: > 0 })
                session.Attacker.WaitingRoom.Add(lane.Attacker);
            if (lane.Defender is { CurrentHp: > 0 })
                session.Defender.WaitingRoom.Add(lane.Defender);
        }

        // Bleedout — all damaged units in Waiting Room take 1 damage
        BleedoutUnits(session.Player1);
        BleedoutUnits(session.Player2);

        // Clean Slate — gain light, discard hand
        CleanSlate(session.Player1, session.GameLog);
        CleanSlate(session.Player2, session.GameLog);

        // Swap attacker/defender
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
        // Gain +1 light automatically
        int lightGained = 1;

        // Gain +1 light for each normal (non-heavy) card discarded
        foreach (var card in player.Hand)
        {
            player.DiscardPile.Add(card);
            lightGained++; // All hand cards grant +1 (Heavy cards never stay in hand as hand cards, they were units)
        }
        player.Hand.Clear();

        player.Light = Math.Min(player.Light + lightGained, player.MaxLight);
        log.Add($"  {player.Username}: +{lightGained} Light → {player.Light}/{player.MaxLight}");
    }

    // ──────────────────────────────────────────────
    // HELPERS
    // ──────────────────────────────────────────────

    private static int RollDie(int dieSize)
    {
        if (dieSize <= 0) return 0;
        return Random.Shared.Next(1, dieSize + 1);
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
