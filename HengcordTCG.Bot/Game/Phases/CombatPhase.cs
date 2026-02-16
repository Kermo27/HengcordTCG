using HengcordTCG.Shared.Models;

namespace HengcordTCG.Bot.Game.Phases;

public static class CombatPhase
{
    private const int LaneCount = 3;

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
                result.AttackerRoll = GameEngine.RollDamage(lane.Attacker.Card.MinDamage, lane.Attacker.Card.MaxDamage);
                result.DefenderRoll = GameEngine.RollDamage(lane.Defender.Card.MinDamage, lane.Defender.Card.MaxDamage);

                if (result.AttackerRoll >= result.DefenderRoll)
                {
                    result.DamageDealt = result.AttackerRoll;
                    lane.Defender.CurrentHp -= result.DamageDealt;
                    result.AttackerWon = true;
                    result.Description = $"Lane {i + 1}: {lane.Attacker.Card.Name} (⚔️{result.AttackerRoll}) beats {lane.Defender.Card.Name} (⚔️{result.DefenderRoll}) — {result.DamageDealt} dmg!";
                }
                else
                {
                    result.DamageDealt = result.DefenderRoll;
                    lane.Attacker.CurrentHp -= result.DamageDealt;
                    result.AttackerWon = false;
                    result.Description = $"Lane {i + 1}: {lane.Defender.Card.Name} (⚔️{result.DefenderRoll}) beats {lane.Attacker.Card.Name} (⚔️{result.AttackerRoll}) — {result.DamageDealt} dmg!";
                }
            }
            else
            {
                result.WasUnopposed = true;
                result.AttackerRoll = GameEngine.RollDamage(lane.Attacker.Card.MinDamage, lane.Attacker.Card.MaxDamage);
                result.DamageDealt = result.AttackerRoll;
                session.Defender.CommanderHp -= result.DamageDealt;
                result.AttackerWon = true;

                result.CounterStrikeDamage = session.Defender.Commander.CounterStrike;
                lane.Attacker.CurrentHp -= result.CounterStrikeDamage;

                result.Description = $"Lane {i + 1}: {lane.Attacker.Card.Name} (⚔️{result.AttackerRoll}) → Commander! {result.DamageDealt} dmg! Counter-strike: {result.CounterStrikeDamage} dmg back!";
            }

            results.Add(result);
        }

        session.LastCombatResults = results;
        session.CurrentPhase = TurnPhase.Resolution;
        session.GameLog.Add("  → Combat resolved");

        return results;
    }
}
