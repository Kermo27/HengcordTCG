using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using HengcordTCG.Bot.Handlers;
using HengcordTCG.Bot.Services;
using HengcordTCG.Shared.Data;
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
        
        // Database
        var dbPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "bot.db");
        services.AddDbContext<AppDbContext>(opt => opt.UseSqlite($"Data Source={dbPath}"));
        
        // Services
        services.AddScoped<UserService>();
        services.AddScoped<ShopService>();
        services.AddScoped<TradeService>();
        
        services.AddHostedService<BotService>();
    })
    .Build();

await host.RunAsync();
