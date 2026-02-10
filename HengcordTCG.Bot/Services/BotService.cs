using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HengcordTCG.Bot.Handlers;

namespace HengcordTCG.Bot.Services;

public class BotService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly InteractionHandler _handler;
    private readonly IConfiguration _config;
    private readonly IServiceProvider _services;
    private readonly ILogger<BotService> _logger;

    public BotService(
        DiscordSocketClient client,
        InteractionService interactions,
        InteractionHandler handler,
        IConfiguration config,
        IServiceProvider services,
        ILogger<BotService> logger)
    {
        _client = client;
        _interactions = interactions;
        _handler = handler;
        _config = config;
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        await _handler.InitializeAsync();

        _client.Log += msg => 
        { 
            _logger.LogInformation("Discord Log: {Message}", msg);
            return Task.CompletedTask; 
        };

        _client.Ready += async () =>
        {
            _logger.LogInformation("Bot connected as {BotUsername}", _client.CurrentUser.Username);
            
            var guildId = _config.GetValue<ulong?>("Discord:GuildId");
            if (guildId.HasValue)
            {
                await _interactions.RegisterCommandsToGuildAsync(guildId.Value);
                _logger.LogInformation("Registered commands to guild: {GuildId}", guildId.Value);
            }
            else
            {
                await _interactions.RegisterCommandsGloballyAsync();
                _logger.LogWarning("Registered commands globally (up to 1h delay). Set 'Discord:GuildId' in appsettings for instant update.");
            }
        };

        var token = _config["Discord:Token"] ?? Environment.GetEnvironmentVariable("DISCORD_TOKEN")
            ?? throw new InvalidOperationException("Discord token not configured.");

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
    }

    public Task StopAsync(CancellationToken ct) => _client.StopAsync();
}
