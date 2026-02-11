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

    [SlashCommand("trade", "Propose a trade with another player")]
    public async Task TradeAsync(
        [Summary("player", "Who do you want to trade with?")] Discord.IUser target,
        [Summary("offer", "What are you offering? (e.g., 'Dragon Sword x1, Gold: 100')")] string offer,
        [Summary("request", "What do you want? (e.g., 'Shield x1')")] string request)
    {
        if (target.IsBot)
        {
            await RespondAsync("🤖 Beep boop. I don't trade with robots.", ephemeral: true);
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
            .WithTitle("🤝 Trade Proposal")
            .WithDescription($"**{Context.User.Username}** proposes a trade with **{target.Username}**!")
            .AddField($"📤 {Context.User.Username} Offers:", FormatTradeDetails(result.OfferContent!))
            .AddField($"📥 {Context.User.Username} Requests:", FormatTradeDetails(result.RequestContent!))
            .WithColor(Color.Orange)
            .Build();

        var components = new ComponentBuilder()
            .WithButton("Accept", $"trade_accept:{trade.Id}", ButtonStyle.Success)
            .WithButton("Reject", $"trade_reject:{trade.Id}", ButtonStyle.Danger)
            .Build();

        await RespondAsync($"{target.Mention}, you have a new trade offer!", embed: embed, components: components);
    }

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

        if (items.Count == 0) return "(Nothing)";
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
                    msg.Content += "\n✅ **TRADE COMPLETED!**";
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
                    msg.Content += "\n⛔ **TRADE CANCELLED.**";
                });
            }
        }
        else
        {
            await RespondAsync($"❌ {result.Message}", ephemeral: true);
        }
    }
}
