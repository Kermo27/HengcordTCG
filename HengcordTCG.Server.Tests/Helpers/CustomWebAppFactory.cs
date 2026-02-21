using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HengcordTCG.Shared.Data;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace HengcordTCG.Server.Tests.Helpers;

public class CustomWebAppFactory : WebApplicationFactory<Program>
{
    public const string TestJwtSecret = "SUPER_SECRET_KEY_FOR_TESTING_PURPOSES_ONLY_123456";
    public const string TestJwtIssuer = "HengcordTCG";
    public const string TestJwtAudience = "HengcordTCG.Web";
    public const string TestApiKey = "test-api-key";

    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.Sources.Clear();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Data Source=:memory:" },
                { "Jwt:Secret", TestJwtSecret },
                { "Jwt:Issuer", TestJwtIssuer },
                { "Jwt:Audience", TestJwtAudience },
                { "ApiKeys:0", TestApiKey },
                { "Discord:ClientId", "test-id" },
                { "Discord:ClientSecret", "test-secret" }
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove ALL EF Core related service descriptors
            var descriptorsToRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType.FullName?.Contains("DbContextOptions") == true ||
                d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") == true ||
                d.ImplementationType?.FullName?.Contains("Sqlite") == true
            ).ToList();

            foreach (var d in descriptorsToRemove)
                services.Remove(d);

            // Add InMemory Database for testing
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });

            // Reconfigure JWT Bearer to use our test secret (overrides Program.cs's eager config reads)
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = TestJwtIssuer,
                    ValidAudience = TestJwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret)),
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    NameClaimType = System.Security.Claims.ClaimTypes.Name
                };
            });
        });
    }
}
