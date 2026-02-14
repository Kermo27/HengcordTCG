# HengcordTCG - Agent Documentation

> **Language Note**: This project uses English for code, comments, and documentation. Some user-facing Discord messages are in Polish.

## Project Overview

HengcordTCG is a full-stack digital trading card game (TCG) ecosystem featuring a Discord bot interface, a Blazor WebAssembly web UI, and a centralized REST API server. The project follows an API-first, decoupled architecture where all business logic and data persistence are handled by the Server, which exposes a RESTful API consumed by client applications (Discord Bot, Blazor Web UI).

### Key Features
- Discord bot with slash commands (`/daily`, `/balance`, `/shop`, `/trade`)
- Card collection and trading system with three rarities (Common, Rare, Legendary)
- Pack-based card acquisition with configurable rarity chances
- SQLite database with Entity Framework Core
- API key authentication for all client-server communication
- Discord OAuth authentication for Web UI
- Interactive API documentation via Scalar

## Technology Stack

- **.NET 10.0** - Target framework for all projects
- **ASP.NET Core 10.0** - Web API framework (Server)
- **Discord.Net 3.18.0** - Discord bot framework
- **Entity Framework Core 9.0.2** - ORM with SQLite provider
- **Scalar.AspNetCore 2.12.36** - Interactive API documentation
- **Blazor WebAssembly 10.0** - Web UI framework
- **AspNet.Security.OAuth.Discord 9.0.0** - Discord OAuth provider
- **System.IdentityModel.Tokens.Jwt 8.x** - JWT authentication
- **SQLite** - Database engine (file-based)

## Project Structure

```
TCGBot/
├── HengcordTCG.sln              # Solution file (5 projects defined)
├── data/                        # SQLite database directory (gitignored)
│   └── bot.db                   # Main database file
├── HengcordTCG.Server/          # ASP.NET Core API Server
│   ├── Program.cs               # Application entry point
│   ├── appsettings.json         # Configuration (gitignored)
│   ├── appsettings.template.json # Configuration template
│   ├── appsettings.Development.json
│   ├── appsettings.Production.json
│   ├── Properties/launchSettings.json  # Launch profiles (port 7156)
│   ├── Controllers/             # API endpoints
│   │   ├── UsersController.cs       # User management, daily rewards
│   │   ├── CardsController.cs       # Card definitions
│   │   ├── CollectionsController.cs # User collections
│   │   ├── ShopController.cs        # Pack purchases
│   │   ├── TradesController.cs      # Trade management
│   │   ├── AdminController.cs       # Admin operations
│   │   └── Web*.cs                  # Web-specific controllers (Auth, Me, Admin, Shop)
│   ├── Middleware/
│   │   ├── ApiKeyAuthMiddleware.cs      # X-API-Key header validation
│   │   ├── RateLimitMiddleware.cs       # Request throttling (60/min, 1000/hr)
│   │   └── GlobalExceptionHandlingMiddleware.cs
│   └── Extensions/
│       └── ValidationExtensions.cs      # Input validation helpers
├── HengcordTCG.Bot/             # Discord Bot (Console Application)
│   ├── Program.cs               # Application entry point
│   ├── appsettings.json         # Configuration (gitignored)
│   ├── appsettings.template.json # Configuration template
│   ├── Commands/                # Slash command modules
│   │   ├── EconomyCommands.cs   # /balance, /daily
│   │   ├── ShopCommands.cs      # /shop commands
│   │   ├── TradeCommands.cs     # /trade commands
│   │   ├── AdminCommands.cs     # Admin-only commands
│   │   ├── InfoCommands.cs      # Information commands
│   │   └── GeneralCommands.cs   # General utility commands
│   ├── Handlers/
│   │   ├── InteractionHandler.cs        # Discord interaction routing
│   │   ├── CardAutocompleteHandler.cs   # Card name autocomplete
│   │   ├── PackAutocompleteHandler.cs   # Pack name autocomplete
│   │   └── RequireBotAdminAttribute.cs  # Admin permission check
│   └── Services/
│       └── BotService.cs        # Discord connection lifecycle
├── HengcordTCG.Shared/          # Shared library (Class Library)
│   ├── Models/                  # Entity Framework entities
│   │   ├── User.cs              # Discord user entity
│   │   ├── Card.cs              # Card definition (Attack, Defense, Rarity)
│   │   ├── UserCard.cs          # User's card inventory (many-to-many)
│   │   ├── PackType.cs          # Card pack definitions with rarity chances
│   │   ├── Trade.cs             # Trade transactions
│   │   ├── TradeContent.cs      # Trade content helper
│   │   └── Rarity.cs            # Enum: Common, Rare, Legendary
│   ├── Data/
│   │   └── AppDbContext.cs      # Entity Framework DbContext
│   ├── Migrations/              # EF Core migrations
│   ├── Clients/
│   │   └── HengcordTCGClient.cs # HTTP client for API communication
│   └── Services/
│       ├── UserService.cs       # User management logic
│       ├── ShopService.cs       # Pack purchase logic
│       └── TradeService.cs      # Trade orchestration
└── HengcordTCG.Blazor/          # Blazor WebAssembly App (Server + Client)
    ├── HengcordTCG.Blazor.csproj           # Server project (hosts the WASM app)
    ├── Program.cs                          # Server entry point
    ├── Properties/launchSettings.json      # Launch profiles (port 5001)
    ├── HengcordTCG.Blazor.Client/          # WebAssembly Client project
    │   ├── Program.cs                      # Client entry point
    │   ├── wwwroot/appsettings.json        # Client configuration (gitignored)
    │   ├── wwwroot/appsettings.template.json # Configuration template
    │   ├── Pages/                          # Razor components
    │   └── Shared/                         # Shared UI components
    └── wwwroot/                            # Static assets
```

## Build and Run Commands

### Prerequisites
- .NET 10.0 SDK
- Discord Bot Token (from Discord Developer Portal)
- Discord Application (for OAuth)

### Configuration Setup

1. **Copy configuration templates:**
```powershell
# Server
cp HengcordTCG.Server/appsettings.template.json HengcordTCG.Server/appsettings.json

# Bot
cp HengcordTCG.Bot/appsettings.template.json HengcordTCG.Bot/appsettings.json

# Blazor Web UI
cp HengcordTCG.Blazor/HengcordTCG.Blazor.Client/wwwroot/appsettings.template.json HengcordTCG.Blazor/HengcordTCG.Blazor.Client/wwwroot/appsettings.json
```

2. **Edit configuration files** - Replace placeholders with actual values.

See [ENV_SETUP.md](ENV_SETUP.md) for detailed configuration options including environment variables and Docker setup.

### Running the Ecosystem

**Start the Server first** (required for Bot and Blazor to function):
```powershell
cd HengcordTCG.Server
dotnet run
# API available at: https://localhost:7156
# Scalar API docs (dev): https://localhost:7156/scalar
```

**Run the Discord Bot**:
```powershell
cd HengcordTCG.Bot
dotnet run
```

**Run the Blazor Web Application**:
```powershell
cd HengcordTCG.Blazor
dotnet run
# Web UI available at: https://localhost:5001
```

**Build all projects:**
```powershell
dotnet build
```

**Apply database migrations manually:**
```powershell
cd HengcordTCG.Server
dotnet ef database update --project ../HengcordTCG.Shared
```

## Architecture Details

### API-First Decoupled Architecture

```
┌─────────────────┐      HTTP + X-API-Key      ┌─────────────────┐
│  HengcordTCG    │◄──────────────────────────►│ HengcordTCG     │
│  .Bot           │                            │  .Server        │
│  (Discord.Net)  │                            │  (ASP.NET Core) │
└─────────────────┘                            └────────┬────────┘
       ▲                                                │
       │                                                │
       │         ┌─────────────────┐                   │
       └────────►│ HengcordTCG     │◄──────────────────┘
                  │  .Shared        │         EF Core
                  │ (Models/Client/  │
                  │   Services)     │
                  └─────────────────┘
                         ▲
                         │
                  ┌──────┴──────┐
                  │ HengcordTCG │
                  │   .Blazor   │
                  │(Blazor WASM)│
                  │   (Web UI)  │
                  └─────────────┘
```

### Authentication Flows

**API Key Authentication (Bot ↔ Server):**
1. Client sends `X-API-Key` header with each request
2. `ApiKeyAuthMiddleware` validates the key against configured `ApiKeys` array
3. Rate limiting is applied per API key or IP address

**Discord OAuth (Web ↔ Server):**
1. Web app redirects to Discord OAuth
2. Server handles callback at `/signin-discord`
3. JWT token issued for authenticated sessions
4. Web uses JWT for subsequent API calls

### Database Schema

**Entities:**
- `User` - Discord users (DiscordId is unique index)
  - Properties: Id, DiscordId, Username, Gold, IsBotAdmin, LastDaily, CreatedAt, LastSeen
- `Card` - Card definitions with rarity, stats, image URL
  - Properties: Id, Name, Attack (0-100), Defense (0-100), Rarity, ImageUrl, ExclusivePackId
- `UserCard` - Many-to-many relationship with count (inventory)
  - Properties: Id, UserId, CardId, Count, ObtainedAt
- `PackType` - Card pack configurations with rarity chances
  - Properties: Id, Name, Price, IsAvailable, ChanceCommon, ChanceRare, ChanceLegendary
- `Trade` - Trade transactions with JSON-serialized card lists
  - Properties: Id, InitiatorId, TargetId, OfferGold, RequestGold, OfferCardsJson, RequestCardsJson, Status, CreatedAt

## Code Style Guidelines

### Language Conventions
- **Code**: English (class names, variables, methods)
- **User-facing messages**: English (Discord responses), some Polish messages in trade system
- **Comments**: English
- **Documentation**: English

### Naming Conventions
- Classes: `PascalCase` (e.g., `HengcordTCGClient`)
- Methods: `PascalCase` (e.g., `GetUserAsync`)
- Private fields: `_camelCase` with underscore prefix
- Configuration keys: `PascalCase` sections with nested objects

### Project Patterns

**Controllers:**
```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    // Constructor injection
    public UsersController(AppDbContext context, UserService userService)
    
    // Use ValidationExtensions for parameter validation
    ValidationExtensions.ValidateDiscordId(discordId);
}
```

**Middleware:**
```csharp
public class CustomMiddleware
{
    private readonly RequestDelegate _next;
    public CustomMiddleware(RequestDelegate next) => _next = next;
    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
}
```

**Services:**
```csharp
public class UserService
{
    private readonly AppDbContext _db;
    private readonly ILogger<UserService> _logger;
    
    // Constructor injection
    public UserService(AppDbContext db, ILogger<UserService> logger)
    
    // Async methods with Async suffix
    public async Task<User> GetOrCreateUserAsync(ulong discordId, string username)
}
```

**Discord Commands:**
```csharp
public class EconomyCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly HengcordTCGClient _client;
    public EconomyCommands(HengcordTCGClient client) => _client = client;

    [SlashCommand("balance", "Check your account balance")]
    public async Task BalanceAsync()
}
```

### Configuration Pattern
```csharp
// Environment variable prefix: HENGCORD_
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables(prefix: "HENGCORD_");
```

## Testing

**Note**: This project does not currently have automated tests. Manual testing workflow:

1. Start the Server
2. Verify API docs at `/scalar`
3. Test endpoints with valid `X-API-Key` header
4. Start the Bot and test Discord commands
5. Check Server logs for request handling

## Security Considerations

### Never Commit Secrets
The following are gitignored:
- `appsettings.json`
- `appsettings.Development.json`
- `appsettings.Production.json`
- `HengcordTCG.Blazor/HengcordTCG.Blazor.Client/wwwroot/appsettings.json`
- `data/*.db*` (SQLite databases)

### API Key Management
- Generate strong random strings or GUIDs for API keys
- Each client (Bot, Web) should have unique keys configured in Server's `ApiKeys` array
- Rotate keys periodically
- Use environment variables in production (see ENV_SETUP.md)

### Discord Token Security
- Store in environment variable `HENGCORD_Discord__Token` or `appsettings.json`
- Never share or commit tokens
- Regenerate token immediately if compromised

### Rate Limiting
- 60 requests per minute per client
- 1000 requests per hour per client
- Identified by API key or IP address

### Input Validation
- Use `ValidationExtensions` for common validations (DiscordId, Username, etc.)
- All API endpoints validate input parameters
- EF Core handles SQL injection protection

## Common Development Tasks

### Adding a New Discord Command
1. Create class in `HengcordTCG.Bot/Commands/`
2. Inherit from `InteractionModuleBase<SocketInteractionContext>`
3. Inject `HengcordTCGClient` for API calls
4. Use `[SlashCommand("name", "English description")]`
5. Build and run - commands auto-register to configured GuildId

### Adding a New API Endpoint
1. Add controller method in `HengcordTCG.Server/Controllers/`
2. Add corresponding method in `HengcordTCG.Shared/Clients/HengcordTCGClient.cs`
3. Use validation extension methods for parameters
4. Test via Scalar UI at `/scalar`

### Adding a Database Migration
```powershell
cd HengcordTCG.Server
dotnet ef migrations add MigrationName --project ../HengcordTCG.Shared
dotnet ef database update --project ../HengcordTCG.Shared
```

### Creating a New Pack Type
Packs are defined in the database via `PackType` entity:
- `Name` - Unique pack identifier
- `Price` - Cost in gold
- `IsAvailable` - Whether pack can be purchased
- `ChanceCommon` / `ChanceRare` / `ChanceLegendary` - Rarity weights (should sum to 100)

Cards can be exclusive to specific packs via `Card.ExclusivePackId`.

## Troubleshooting

### Database locked errors
- Ensure single Server instance accesses the database
- Check for leftover `.db-shm` and `.db-wal` files

### Discord commands not appearing
- Verify `Discord:GuildId` is set for instant registration
- Global commands take up to 1 hour to propagate
- Check Bot has `applications.commands` scope

### API 401 Unauthorized
- Verify `X-API-Key` header is present and matches Server configuration
- Check that header name is exactly `X-API-Key` (case-sensitive)

### Migrations not applying
- Check database path in logs during startup
- Ensure `HENGCORD_DATA_DIR` environment variable is set correctly if using Docker

## Deployment Notes

### Production Configuration
- Use environment variables instead of `appsettings.json`
- Set `ASPNETCORE_ENVIRONMENT=Production`
- Configure absolute paths for database and assets
- Set up HTTPS reverse proxy
- Configure CORS origins appropriately

### Docker Environment Variables
```bash
# Server
HENGCORD_ApiKeys__0=bot_api_key
HENGCORD_ApiKeys__1=blazor_api_key
HENGCORD_ConnectionStrings__DefaultConnection="Data Source=/data/bot.db"
HENGCORD_DATA_DIR=/data

# Bot
HENGCORD_Discord__Token=discord_token
HENGCORD_Discord__GuildId=guild_id
HENGCORD_ServerUrl=https://server:7156
HENGCORD_ApiKey=bot_api_key

# Blazor Web UI (configured via wwwroot/appsettings.json)
# ApiBaseUrl: https://server:7156
# ApiKey: blazor_api_key
```
