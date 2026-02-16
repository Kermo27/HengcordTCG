using System.Net.Http;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using HengcordTCG.Bot.Handlers;
using HengcordTCG.Bot.Game;
using HengcordTCG.Bot.Services;
using HengcordTCG.Shared.Clients;
using HengcordTCG.Shared.Services;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables(prefix: "HENGCORD_");
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
        
        // API Client with API Key authentication
        var serverUrl = context.Configuration["ServerUrl"] ?? "https://localhost:7193";
        var botApiKey = context.Configuration["ApiKey"] ?? "YOUR_BOT_API_KEY";
        services.AddHttpClient<HengcordTCGClient>(client =>
        {
            client.BaseAddress = new Uri(serverUrl);
            client.DefaultRequestHeaders.Add("X-API-Key", botApiKey);
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            // Bypass SSL certificate validation for localhost (development)
            if (serverUrl.Contains("localhost"))
            {
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            return handler;
        });

        // Game system
        services.AddSingleton<GameManager>();
        services.AddTransient<GameButtonHandler>();
        
        services.AddHostedService<BotService>();
    })
    .Build();

await host.RunAsync();
