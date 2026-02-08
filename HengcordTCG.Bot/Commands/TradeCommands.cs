using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using HengcordTCG.Shared.Services;
using System.Collections.Generic;
using System.Linq;

namespace HengcordTCG.Bot.Commands;

public class TradeCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly TradeService _tradeService;

    public TradeCommands(TradeService tradeService)
    {
        _tradeService = tradeService;
    }

    [SlashCommand("trade", "Zaproponuj wymianę z innym graczem")]
    public async Task TradeAsync(
        [Summary("gracz", "Z kim chcesz się wymienić?")] Discord.IUser target,
        [Summary("oferta", "Co dajesz? (np. 'Smoczy Miecz x1, Gold: 100')")] string offer,
        [Summary("zadanie", "Czego oczekujesz? (np. 'Tarcza x1')")] string request)
    {
        if (target.IsBot)
        {
            await RespondAsync("🤖 Bipy boop. Nie wymieniam się z robotami.", ephemeral: true);
            return;
        }

        var result = await _tradeService.CreateTradeAsync(Context.User.Id, Context.User.Username, target.Id, target.Username, offer, request);

        if (!result.success)
        {
            await RespondAsync($"❌ {result.message}", ephemeral: true);
            return;
        }

        var trade = result.trade!;

        var embed = new EmbedBuilder()
            .WithTitle("🤝 Propozycja Wymiany")
            .WithDescription($"**{Context.User.Username}** proponuje wymianę z **{target.Username}**!")
            .AddField($"📤 {Context.User.Username} Oferuje:", FormatTradeDetails(result.offerContent!))
            .AddField($"📥 {Context.User.Username} Oczekuje:", FormatTradeDetails(result.requestContent!))
            .WithColor(Color.Orange)
            .Build();

        var components = new ComponentBuilder()
            .WithButton("Akceptuj", $"trade_accept:{trade.Id}", ButtonStyle.Success)
            .WithButton("Odrzuć", $"trade_reject:{trade.Id}", ButtonStyle.Danger)
            .Build();

        await RespondAsync($"{target.Mention}, masz nową ofertę wymiany!", embed: embed, components: components);
    }

    // ... handler methods ...

    private string FormatTradeDetails(TradeService.TradeContent content)
    {
        var items = new List<string>();
        if (content.Gold > 0) items.Add($"💰 **{content.Gold} Gold**");

        if (content.CardNames != null)
        {
            foreach (var kvp in content.CardNames)
            {
                items.Add($"🃏 **{kvp.Key}** (x{kvp.Value})");
            }
        }

        if (items.Count == 0) return "(Nic)";
        return string.Join("\n", items);
    }

    [ComponentInteraction("trade_accept:*")]
    public async Task AcceptTrade(string tradeIdStr)
    {
        if (!int.TryParse(tradeIdStr, out int tradeId)) return;

        var result = await _tradeService.AcceptTradeAsync(tradeId, Context.User.Id);

        if (result.success)
        {
            await RespondAsync($"✅ {result.message}");
            // Update original message to remove buttons
            if (Context.Interaction is SocketMessageComponent component)
            {
                await component.Message.ModifyAsync(msg => {
                    msg.Components = new ComponentBuilder().Build(); // Clear buttons
                    msg.Content += "\n✅ **WYMIANA ZAKOŃCZONA!**";
                });
            }
        }
        else
        {
            await RespondAsync($"❌ {result.message}", ephemeral: true);
        }
    }

    [ComponentInteraction("trade_reject:*")]
    public async Task RejectTrade(string tradeIdStr)
    {
        if (!int.TryParse(tradeIdStr, out int tradeId)) return;

        var result = await _tradeService.RejectTradeAsync(tradeId, Context.User.Id);

        if (result.success)
        {
            await RespondAsync($"⛔ {result.message}");
            
            if (Context.Interaction is SocketMessageComponent component)
            {
                await component.Message.ModifyAsync(msg => {
                    msg.Components = new ComponentBuilder().Build();
                    msg.Content += "\n⛔ **WYMIANA ODWOŁANA.**";
                });
            }
        }
        else
        {
            await RespondAsync($"❌ {result.message}", ephemeral: true);
        }
    }


}
