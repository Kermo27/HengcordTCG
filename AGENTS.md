# HengcordTCG - AI Agent Documentation

> **Language Note**: Project documentation is primarily in Polish (DOKUMENTACJA.md) and English (README.md). Code comments and strings are mixed Polish/English. This file is written in English for AI agent consumption.

## Project Overview

HengcordTCG is a full-stack digital Trading Card Game (TCG) ecosystem with a Discord bot interface and web dashboard. It uses an API-first architecture where all business logic resides in a centralized ASP.NET Core server.

### Architecture Diagram

```
┌─────────────────┐     HTTP + API Key      ┌─────────────────────────────┐
│  HengcordTCG    │◄───────────────────────►│      HengcordTCG.Server     │
│  .Bot (Discord) │                         │    (ASP.NET Core API)       │
└─────────────────┘                         └──────────────┬──────────────┘
                                                           │
┌─────────────────┐     HTTP + JWT/Cookie    ┌─────────────▼──────────────┐
│  HengcordTCG    │◄────────────────────────►│      SQLite Database       │
│ .Blazor (Web)   │                          │      (hengcordtcg.db)      │
└─────────────────┘                          └────────────────────────────┘
         │                                              │
         └──────────────────┬───────────────────────────┘
                            │
                   ┌────────▼─────────┐
                   │ HengcordTCG.Shared│
                   │  (Models/Client)  │
                   └───────────────────┘
```

## Technology Stack

| Component | Technology | Version |
|-----------|------------|---------|
| **Server** | ASP.NET Core | .NET 10.0 |
| **Bot** | Discord.Net | 3.18.0 |
| **Web UI** | Blazor WebAssembly | .NET 10.0 |
| **Database** | SQLite + EF Core | 9.0.2 |
| **UI Library** | BlazorBlueprint | 2.x |
| **API Docs** | Scalar | 2.12.36 |
| **Auth** | JWT Bearer + Discord OAuth | - |

## Project Structure

```
TCGBot/
├── HengcordTCG.sln                    # Solution file
│
├── HengcordTCG.Shared/                # Shared library (all projects reference this)
│   ├── Models/                        # EF Core entities
│   │   ├── Card.cs                    # Card definition
│   │   ├── User.cs                    # Player data
│   │   ├── Deck.cs                    # Battle deck
│   │   ├── Trade.cs                   # Trade system
│   │   ├── Wiki.cs                    # Wiki pages
│   │   └── ...
│   ├── Data/
│   │   └── AppDbContext.cs            # EF Core DbContext
│   ├── Clients/
│   │   └── HengcordTCGClient.cs       # HTTP client for API
│   ├── Services/                      # Shared business logic
│   └── Migrations/                    # EF Core migrations
│
├── HengcordTCG.Server/                # ASP.NET Core API
│   ├── Controllers/                   # API endpoints
│   │   ├── UsersController.cs
│   │   ├── CardsController.cs
│   │   ├── ShopController.cs
│   │   ├── TradesController.cs
│   │   ├── WikiController.cs
│   │   └── ...
│   ├── Middleware/                    # Custom middleware
│   │   ├── ApiKeyAuthMiddleware.cs    # API key validation
│   │   ├── RateLimitMiddleware.cs
│   │   └── GlobalExceptionHandlingMiddleware.cs
│   ├── Authentication/
│   │   └── ApiKeyAuthenticationHandler.cs
│   ├── appsettings.json               # Config (gitignored, use template)
│   └── Program.cs
│
├── HengcordTCG.Bot/                   # Discord Bot
│   ├── Commands/                      # Slash commands
│   │   ├── GameCommands.cs            # /game deck, /game challenge
│   │   ├── ShopCommands.cs            # /shop, /buy
│   │   ├── TradeCommands.cs           # /trade
│   │   ├── EconomyCommands.cs         # /daily, /balance
│   │   └── AdminCommands.cs           # Admin commands
│   ├── Handlers/                      # Event handlers
│   │   ├── InteractionHandler.cs      # Command routing
│   │   ├── GameButtonHandler.cs       # Battle UI buttons
│   │   └── CardAutocompleteHandler.cs # Card name autocomplete
│   ├── Game/                          # Battle game engine
│   │   ├── GameManager.cs
│   │   ├── GameSession.cs
│   │   └── GameState.cs
│   ├── Services/
│   │   └── BotService.cs              # Discord connection
│   ├── appsettings.json               # Bot token, API key (gitignored)
│   └── Program.cs
│
└── HengcordTCG.Blazor/                # Web UI (Blazor)
    ├── HengcordTCG.Blazor.Client/     # WASM client
    │   ├── Pages/                     # Razor pages
    │   │   ├── Index.razor
    │   │   ├── Collection.razor
    │   │   ├── Shop.razor
    │   │   ├── Wiki.razor
    │   │   ├── Trades.razor
    │   │   └── Admin/
    │   ├── Services/
    │   │   ├── AuthService.cs
    │   │   └── WikiService.cs
    │   └── Shared/                    # UI components
    ├── wwwroot/
    │   └── appsettings.json           # API URL config
    └── Program.cs
```

## Build and Run Commands

### Prerequisites
- .NET 10.0 SDK
- Discord Bot Token (get from Discord Developer Portal)

### Setup Configuration

1. **Copy template configs** (from project root):
```powershell
cp HengcordTCG.Server/appsettings.template.json HengcordTCG.Server/appsettings.json
cp HengcordTCG.Bot/appsettings.template.json HengcordTCG.Bot/appsettings.json
cp HengcordTCG.Blazor/HengcordTCG.Blazor.Client/wwwroot/appsettings.template.json HengcordTCG.Blazor/HengcordTCG.Blazor.Client/wwwroot/appsettings.json
```

2. **Edit configs** with your secrets (see ENV_SETUP.md)

### Running the Ecosystem

**Start order matters** - Server must be running before Bot/Blazor:

```powershell
# Terminal 1: Start Server (required first)
cd HengcordTCG.Server
dotnet run
# API: https://localhost:7156
# Docs: https://localhost:7156/scalar

# Terminal 2: Start Bot
cd HengcordTCG.Bot
dotnet run

# Terminal 3: Start Web UI (optional)
cd HengcordTCG.Blazor
dotnet run
# Web: https://localhost:5001
```

### Build Commands

```powershell
# Build entire solution
dotnet build HengcordTCG.sln

# Build specific project
dotnet build HengcordTCG.Server
dotnet build HengcordTCG.Bot
dotnet build HengcordTCG.Blazor

# Run with specific configuration
dotnet run --configuration Release
```

## Database Migrations

Migrations are stored in `HengcordTCG.Shared/Migrations/`:

```powershell
# Add new migration (run from HengcordTCG.Server or Shared)
cd HengcordTCG.Server
dotnet ef migrations add MigrationName --project ../HengcordTCG.Shared

# Apply migrations (auto-applied on server startup)
dotnet ef database update --project ../HengcordTCG.Shared
```

## Key Domain Concepts

### Card System
- **Card Types**: `Unit`, `Commander`, `Closer`
- **Rarities**: `Common`, `Rare`, `Legendary`
- **Stats**: Attack, Defense, Health, LightCost, MinDamage, MaxDamage, Speed, CounterStrike
- **Abilities**: AbilityText (display), AbilityId (engine logic)

### Battle System (PvP)
- Players configure a **Deck** with:
  - 1 Commander (leader with HP pool)
  - 9 Main Deck cards
  - 3 Closer cards (finishers)
- Turn-based combat with Light resource system
- Match results persisted to database

### Economy
- **Gold** - Primary currency
- **Daily rewards** - `/daily` command
- **Packs** - Buy random cards
- **Trading** - Player-to-player card exchange

### Wiki System
- Hierarchical pages (parent/child)
- User proposals for changes
- Admin approval workflow
- Markdown content support

## Authentication & Security

### API Key Authentication (Bot → Server)
- Header: `X-API-Key: your-bot-api-key`
- Configured in `appsettings.json` for both sides
- Multiple keys supported (array in Server config)

### JWT Authentication (Web → Server)
- Discord OAuth flow
- JWT tokens with 7-day expiry
- Roles: `Admin` for admin panel access

### Environment Variables
Prefix: `HENGCORD_`

```powershell
# Server
$env:HENGCORD_ApiKeys__0="bot-api-key"
$env:HENGCORD_ApiKeys__1="web-api-key"
$env:HENGCORD_ConnectionStrings__DefaultConnection="Data Source=/data/bot.db"

# Bot
$env:HENGCORD_Discord__Token="discord-bot-token"
$env:HENGCORD_Discord__GuildId="guild-id"
$env:HENGCORD_ApiKey="bot-api-key"
$env:HENGCORD_ServerUrl="https://localhost:7156"
```

## Code Style Guidelines

### C# Conventions
- **Implicit usings**: Enabled (`ImplicitUsings` in csproj)
- **Nullable**: Enabled (`<Nullable>enable</Nullable>`)
- **Target Framework**: `net10.0`
- **File-scoped namespaces** preferred
- **Primary constructors** for simple services

### Project References
```
HengcordTCG.Server -> HengcordTCG.Shared
HengcordTCG.Bot -> HengcordTCG.Shared
HengcordTCG.Blazor.Client -> HengcordTCG.Shared
HengcordTCG.Blazor -> HengcordTCG.Blazor.Client
```

### API Controller Pattern
```csharp
[ApiController]
[Route("api/[controller]")]
public class ExampleController : ControllerBase
{
    // Use shared services from DI
    // Return typed ActionResult<T>
    // Validate inputs with ValidationExtensions
}
```

## Testing

**Note**: No formal test projects exist yet. Testing is manual:

1. Start Server
2. Check Scalar docs: `https://localhost:7156/scalar`
3. Test Bot commands in Discord
4. Test Web UI at `https://localhost:5001`

## Common Development Tasks

### Adding a New API Endpoint
1. Add controller method in `HengcordTCG.Server/Controllers/`
2. Add client method in `HengcordTCG.Shared/Clients/HengcordTCGClient.cs`
3. Use in Bot commands or Blazor pages

### Adding a New Model/Entity
1. Create model in `HengcordTCG.Shared/Models/`
2. Add `DbSet` to `AppDbContext.cs`
3. Configure relationships in `OnModelCreating`
4. Create and apply migration

### Adding a Discord Command
1. Create/modify command class in `HengcordTCG.Bot/Commands/`
2. Inherit from `InteractionModuleBase<SocketInteractionContext>`
3. Use `[SlashCommand]` and `[Group]` attributes
4. Inject `HengcordTCGClient` for API calls

## Important File Locations

| Purpose | Path |
|---------|------|
| Database | `HengcordTCG.Server/hengcordtcg.db` (auto-created) |
| Card Images | `HengcordTCG.Server/wwwroot/images/cards/` |
| Server Config | `HengcordTCG.Server/appsettings.json` (gitignored) |
| Bot Config | `HengcordTCG.Bot/appsettings.json` (gitignored) |
| Web Config | `HengcordTCG.Blazor/HengcordTCG.Blazor.Client/wwwroot/appsettings.json` (gitignored) |

## Troubleshooting

### SSL Certificate Issues (Development)
Bot bypasses SSL validation for localhost - see `HengcordTCG.Bot/Program.cs`:
```csharp
handler.ServerCertificateCustomValidationCallback = 
    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
```

### Database Locked
SQLite doesn't support concurrent writes well. Ensure only one Server instance runs.

### Bot Commands Not Appearing
- Check Discord `GuildId` in config
- Bot needs `applications.commands` scope
- May need to re-register commands (happens on startup)

## IDE Configuration

### JetBrains Rider
- `.idea/` directory is gitignored
- Run configurations exist in `.idea/.idea.HengcordTCG/.idea/runConfigurations/`
  - `Server.xml`
  - `Bot.xml`
  - `Blazor.xml`
  - `Compound__Server_Bot_Blazor.xml` (runs all three)

---

*Last updated: 2026-02-16*
