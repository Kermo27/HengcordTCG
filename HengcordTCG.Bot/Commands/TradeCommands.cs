using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.Clients;
using System.Collections.Generic;
using System.Linq;

namespace HengcordTCG.Bot.Commands;

public class TradeCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly HengcordTCGClient _client;

    public TradeCommands(HengcordTCGClient client)
    {
        _client = client;
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

        var result = await _client.CreateTradeAsync(new HengcordTCGClient.CreateTradeRequest(
            Context.User.Id, Context.User.Username, target.Id, target.Username, offer, request
        ));

        if (!result.Success || result.Trade == null)
        {
            await RespondAsync($"❌ {result.Message}", ephemeral: true);
            return;
        }

        var trade = result.Trade;

        var embed = new EmbedBuilder()
            .WithTitle("🤝 Propozycja Wymiany")
            .WithDescription($"**{Context.User.Username}** proponuje wymianę z **{target.Username}**!")
            .AddField($"📤 {Context.User.Username} Oferuje:", FormatTradeDetails(result.OfferContent!))
            .AddField($"📥 {Context.User.Username} Oczekuje:", FormatTradeDetails(result.RequestContent!))
            .WithColor(Color.Orange)
            .Build();

        var components = new ComponentBuilder()
            .WithButton("Akceptuj", $"trade_accept:{trade.Id}", ButtonStyle.Success)
            .WithButton("Odrzuć", $"trade_reject:{trade.Id}", ButtonStyle.Danger)
            .Build();

        await RespondAsync($"{target.Mention}, masz nową ofertę wymiany!", embed: embed, components: components);
    }

    // ... handler methods ...

    private string FormatTradeDetails(TradeContent content)
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

        var result = await _client.AcceptTradeAsync(tradeId, Context.User.Id);

        if (result.Success)
        {
            await RespondAsync($"✅ {result.Message}");
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
            await RespondAsync($"❌ {result.Message}", ephemeral: true);
        }
    }

    [ComponentInteraction("trade_reject:*")]
    public async Task RejectTrade(string tradeIdStr)
    {
        if (!int.TryParse(tradeIdStr, out int tradeId)) return;

        var result = await _client.RejectTradeAsync(tradeId, Context.User.Id);

        if (result.Success)
        {
            await RespondAsync($"⛔ {result.Message}");
            
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
            await RespondAsync($"❌ {result.Message}", ephemeral: true);
        }
    }


}
