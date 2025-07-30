using IdentityServer4Try5.Data;
using IdentityServer4Try5.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Mappers;
using IdentityServer4.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure DbContext for Identity and IdentityServer
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Configure IdentityServer
builder.Services.AddIdentityServer()
    .AddAspNetIdentity<ApplicationUser>()
    .AddConfigurationStore(options =>
    {
        options.ConfigureDbContext = b => b.UseNpgsql(connectionString,
            sql => sql.MigrationsAssembly("IdentityServer4Try5"));
    })
    .AddOperationalStore(options =>
    {
        options.ConfigureDbContext = b => b.UseNpgsql(connectionString,
            sql => sql.MigrationsAssembly("IdentityServer4Try5"));
    })
    .AddDeveloperSigningCredential();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseIdentityServer();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed IdentityServer configuration
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var configuration = builder.Configuration;
    //var configuration = services.GetRequiredService<IConfiguration>();
    try
    {
        var configurationDbContext = services.GetRequiredService<ConfigurationDbContext>();
        Console.WriteLine("Applying ConfigurationDbContext migrations...");
        configurationDbContext.Database.Migrate();
        Console.WriteLine("ConfigurationDbContext migrations applied.");

        var clients = new[]
        {
            new Client
            {
                ClientId = "client1",
                ClientName = "Client App 1",
                AllowedGrantTypes = GrantTypes.Code,
                ClientSecrets = { new Secret("secret".Sha256()) },
                RedirectUris = { configuration["OIDC:Client1:RedirectUri"] },
                PostLogoutRedirectUris = { configuration["OIDC:Client1:PostLogoutRedirectUri"] },
                AllowedScopes = { "openid", "profile", "api1" },
                RequireConsent = false
            },
            new Client
            {
                ClientId = "client2",
                ClientName = "Client App 2",
                AllowedGrantTypes = GrantTypes.Code,
                ClientSecrets = { new Secret("secret".Sha256()) },
                RedirectUris = { configuration["OIDC:Client2:RedirectUri"] },
                PostLogoutRedirectUris = { configuration["OIDC:Client2:PostLogoutRedirectUri"] },
                AllowedScopes = { "openid", "profile", "api1" },
                RequireConsent = false
            }
        };

        foreach (var client in clients)
        {
            if (!configurationDbContext.Clients.Any(c => c.ClientId == client.ClientId))
            {
                configurationDbContext.Clients.Add(client.ToEntity());
            }
        }

        var identityResources = new[]
        {
            new IdentityResource("openid", new[] { "sub" }),
            new IdentityResource("profile", new[] { "name", "email" })
        };

        foreach (var resource in identityResources)
        {
            if (!configurationDbContext.IdentityResources.Any(r => r.Name == resource.Name))
            {
                configurationDbContext.IdentityResources.Add(resource.ToEntity());
            }
        }

        var apiScopes = new[]
        {
            new ApiScope("api1", "My API")
        };

        foreach (var apiScope in apiScopes)
        {
            if (!configurationDbContext.ApiScopes.Any(s => s.Name == apiScope.Name))
            {
                configurationDbContext.ApiScopes.Add(apiScope.ToEntity());
            }
        }

        configurationDbContext.SaveChanges();
        Console.WriteLine("Configuration data seeded.");

        var persistedGrantDbContext = services.GetRequiredService<PersistedGrantDbContext>();
        Console.WriteLine("Applying PersistedGrantDbContext migrations...");
        persistedGrantDbContext.Database.Migrate();
        Console.WriteLine("PersistedGrantDbContext migrations applied.");

        var appDbContext = services.GetRequiredService<ApplicationDbContext>();
        Console.WriteLine("Applying ApplicationDbContext migrations...");
        appDbContext.Database.Migrate();
        Console.WriteLine("ApplicationDbContext migrations applied.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration or seeding error: {ex.Message}");
        throw;
    }
}

app.Run();