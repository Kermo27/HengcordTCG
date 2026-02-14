using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using HengcordTCG.Shared.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HengcordTCG.Bot.Handlers;

public class InteractionHandler
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly IServiceProvider _services;
    private readonly ILogger<InteractionHandler> _logger;

    public InteractionHandler(DiscordSocketClient client, InteractionService interactions, IServiceProvider services, ILogger<InteractionHandler> logger)
    {
        _client = client;
        _interactions = interactions;
        _services = services;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await _interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);

        _client.InteractionCreated += HandleInteractionAsync;
        _interactions.SlashCommandExecuted += SlashCommandExecutedAsync;
        
        _logger.LogInformation("InteractionHandler initialized successfully");
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        // Auto-register the user before any command executes
        try
        {
            var apiClient = _services.GetRequiredService<HengcordTCGClient>();
            await apiClient.GetOrCreateUserAsync(interaction.User.Id, interaction.User.Username);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-sync user {UserId} before command", interaction.User.Id);
        }

        var context = new SocketInteractionContext(_client, interaction);
        await _interactions.ExecuteCommandAsync(context, _services);
    }

    private Task SlashCommandExecutedAsync(SlashCommandInfo command, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess)
        {
            _logger.LogError("Command execution failed - Command: {CommandName}, Error: {ErrorReason}, User: {UserId}", 
                command.Name, result.ErrorReason, context.User.Id);
        }
        return Task.CompletedTask;
    }
}
