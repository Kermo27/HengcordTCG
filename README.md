# HengcordTCG - Digital Trading Card Game Ecosystem

HengcordTCG is a full-stack digital trading card game ecosystem featuring a Discord bot interface, Blazor WebAssembly frontend, and a centralized API server.

## Architecture Overview

The project is built using a decoupled, API-first architecture. All business logic and data persistence are handled by the Server, which exposes a RESTful API consumed by other components.

```
┌─────────────────────────────────────────────────────────────────┐
│                          Clients                                │
├─────────────────┬─────────────────┬─────────────────────────────┤
│  Discord Bot    │  Blazor Web     │  Admin Panel (Web)          │
│  (Discord.Net)  │  (WebAssembly)  │  (Role-based access)        │
└────────┬────────┴────────┬────────┴─────────────────────────────┘
         │                 │
         │  HTTP + API Key │  HTTP + JWT (Discord OAuth)
         │                 │
         ▼                 ▼
┌─────────────────────────────────────────────────────────────────┐
│                    HengcordTCG.Server                           │
│                    ASP.NET Core 10.0                            │
├─────────────────────────────────────────────────────────────────┤
│  • REST API          • Discord OAuth                            │
│  • Entity Framework  • SQLite Database                          │
│  • Card Generation   • Wiki System                              │
└─────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────────┐
│                    HengcordTCG.Shared                           │
│  • Models (User, Card, Pack, Trade, Wiki, Match)                │
│  • Services (UserService, ShopService, WikiService, etc.)       │
│  • HengcordTCGClient (API client for all consumers)             │
└─────────────────────────────────────────────────────────────────┘
```

## Project Components

### HengcordTCG.Server
The backbone of the system.
- **Technology**: ASP.NET Core 10.0, Entity Framework Core, SQLite
- **Features**:
  - RESTful API with JWT + API Key authentication
  - Discord OAuth integration for web users
  - Card image generation
  - Wiki system with proposal workflow
- **API Docs**: Interactive documentation via Scalar (Development mode)

### HengcordTCG.Bot
Discord bot for game interaction.
- **Technology**: Discord.Net
- **Commands**:
  - `/daily` - Claim daily gold rewards
  - `/balance` - Check your gold
  - `/shop` - Buy card packs
  - `/trade` - Trade cards with other players
  - `/match` - Challenge other players to card battles
  - `/wiki` - View wiki pages
- **Game System**: Real-time card battles with phases (Strategy, Declaration, Combat, Resolution)

### HengcordTCG.Blazor
Web frontend for the game.
- **Technology**: Blazor WebAssembly, Tailwind CSS v4
- **Pages**:
  - **Home** - Stats, leaderboard, daily rewards
  - **Collection** - View and manage your card collection
  - **Shop** - Purchase card packs
  - **Trades** - Propose and accept card trades
  - **Wiki** - Browse and edit game documentation
  - **Deck** - Build decks for matches
  - **Admin Panel** - Manage cards, packs, users, wiki proposals
- **Features**:
  - Discord OAuth login
  - Responsive design (mobile + desktop)
  - Dark theme
  - Live markdown preview with diff comparison for wiki edits

### HengcordTCG.Shared
Shared library used by all projects.
- **Models**: User, Card, PackType, UserCard, Trade, Wiki, WikiProposal, Deck, MatchResult
- **Services**: UserService, ShopService, TradeService, WikiService, WikiProposalService
- **HengcordTCGClient**: Typed HTTP client for API communication
- **Migrations**: EF Core database migrations

## Features

### For Players
- Collect cards by opening packs
- Trade cards with other players
- Build custom decks
- Battle other players via Discord
- Daily gold rewards
- Contribute to the wiki

### For Administrators
- Manage cards and packs via Admin Panel
- Review and approve/reject wiki proposals
- Manage users (set admin status, adjust gold)
- View system statistics

### Wiki System
- Create, edit, and delete pages (via proposals)
- Side-by-side diff view for edit proposals
- Markdown editor with live preview
- Version history

## Tech Stack

| Component | Technology |
|-----------|------------|
| Backend | ASP.NET Core 10.0 |
| Database | SQLite with Entity Framework Core |
| Discord Bot | Discord.Net |
| Frontend | Blazor WebAssembly |
| CSS | Tailwind CSS v4 |
| Icons | Font Awesome |
| Auth | Discord OAuth + JWT |
| Diff | DiffPlex |

## Setup Instructions

### Prerequisites
- .NET 10.0 SDK
- Discord Bot Token (for Discord integration)
- Discord OAuth Application (for web login)

### Configuration

1. Copy template files:
   ```bash
   cp HengcordTCG.Server/appsettings.template.json HengcordTCG.Server/appsettings.json
   cp HengcordTCG.Bot/appsettings.template.json HengcordTCG.Bot/appsettings.json
   cp HengcordTCG.Blazor/HengcordTCG.Blazor.Client/wwwroot/appsettings.template.json HengcordTCG.Blazor/HengcordTCG.Blazor.Client/wwwroot/appsettings.json
   ```

2. Configure `appsettings.json` files with your credentials.

### Running Locally

1. **Start the Server**:
   ```bash
   cd HengcordTCG.Server
   dotnet run
   ```
   - API: `https://localhost:7156`
   - API Docs: `https://localhost:7156/scalar`

2. **Run the Discord Bot** (optional):
   ```bash
   cd HengcordTCG.Bot
   dotnet run
   ```

3. **Run the Blazor Frontend**:
   ```bash
   cd HengcordTCG.Blazor
   dotnet run
   ```
   - Web: `https://localhost:5001`

### Production Deployment

See `deploy/` folder for:
- `hengcordtcg.service` - Systemd service configuration
- `nginx.conf` - Nginx reverse proxy configuration

Required GitHub Secrets for CI/CD:
- `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY`
- `DISCORD_CLIENT_ID`, `DISCORD_CLIENT_SECRET`
- `JWT_SECRET`, `BOT_API_KEY`, `WEB_API_KEY`

## Database

- Uses SQLite for simplicity
- Automatic migrations on startup
- Database file: `data/hengcordtcg.db` (or configured path)

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

---
*Created by the HengcordTCG Team*
