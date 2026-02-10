using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using HengcordTCG.Bot.Handlers;
using HengcordTCG.Bot.Services;
using HengcordTCG.Shared.Clients;
using HengcordTCG.Shared.Services;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.AddJsonFile("appsettings.json", optional: true);
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        // Discord
        services.AddSingleton(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
        });
        services.AddSingleton<DiscordSocketClient>();
        services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
        services.AddSingleton<InteractionHandler>();
        
        // API Client
        var serverUrl = context.Configuration["ServerUrl"] ?? "https://localhost:7193";
        services.AddHttpClient<HengcordTCGClient>(client =>
        {
            client.BaseAddress = new Uri(serverUrl);
        });

        // Some services might still be needed locally if they don't use DB, 
        // but for now we focus on the API Client.
        // CardImageService still needs local assets or we move it to server?
        // User requested bot to use server, so we should probably move image gen to server too later.
        // For now, let's keep it scoped if it's used in commands.
        services.AddScoped<CardImageService>(sp => new CardImageService(AppContext.BaseDirectory));
        
        services.AddHostedService<BotService>();
    })
    .Build();

await host.RunAsync();
