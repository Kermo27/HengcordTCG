# Environment Variables Configuration

This project uses environment variables for sensitive configuration. Never commit secrets to Git.

## Setup Guide

### Using appsettings.json (Development Only)

1. Copy `appsettings.template.json` to `appsettings.json` in each project:
   ```bash
   # For Server
   cp HengcordTCG.Server/appsettings.template.json HengcordTCG.Server/appsettings.json
   
   # For Bot
   cp HengcordTCG.Bot/appsettings.template.json HengcordTCG.Bot/appsettings.json
   
   # For Web
   cp HengcordTCG.Web/appsettings.template.json HengcordTCG.Web/appsettings.json
   ```

2. Edit each `appsettings.json` and replace placeholders:
   - `YOUR_DISCORD_BOT_TOKEN` → Your Discord Bot Token
   - `YOUR_DISCORD_CLIENT_ID` → Your Discord App Client ID
   - `YOUR_DISCORD_CLIENT_SECRET` → Your Discord App Client Secret
   - `YOUR_BOT_API_KEY` → Your Bot API Key (generate: `guid` or strong random string)
   - `YOUR_WEB_API_KEY` → Your Web API Key (generate: `guid` or strong random string)

### Using Environment Variables (Production Recommended)

Set environment variables with prefix `HENGCORD_`:

```bash
# For Server
export HENGCORD_ApiKeys__0=bot_api_key_here
export HENGCORD_ApiKeys__1=web_api_key_here
export HENGCORD_ConnectionStrings__DefaultConnection="Data Source=/data/bot.db"

# For Bot
export HENGCORD_Discord__Token=your_discord_token
export HENGCORD_Discord__GuildId=your_guild_id
export HENGCORD_ServerUrl=http://localhost:5266
export HENGCORD_ApiKey=bot_api_key_here

# For Web
export HENGCORD_Discord__ClientId=your_client_id
export HENGCORD_Discord__ClientSecret=your_client_secret
export HENGCORD_ServerUrl=http://localhost:5266
export HENGCORD_ApiKey=web_api_key_here
```

### Docker / Container Setup

Use `.env` file or pass `-e` flags:

```dockerfile
ENV HENGCORD_ApiKeys__0=bot_api_key
ENV HENGCORD_ApiKeys__1=web_api_key
ENV HENGCORD_Discord__Token=discord_token
```

Or with docker-compose:

```yaml
environment:
  - HENGCORD_ApiKeys__0=bot_api_key
  - HENGCORD_ApiKeys__1=web_api_key
  - HENGCORD_Discord__Token=discord_token
```

## Notes

- All secrets in `appsettings.json` are in `.gitignore` and won't be committed
- Template files (`appsettings.template.json`) show required configuration keys
- For array values, use `__0`, `__1`, etc. (e.g., `HENGCORD_ApiKeys__0`)
- Prefix `HENGCORD_` can be modified in Program.cs if needed
