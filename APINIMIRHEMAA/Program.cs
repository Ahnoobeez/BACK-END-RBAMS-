using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Apply module-access authorization to controller actions through the shared filter.
builder.Services.AddControllers(options =>
    options.Filters.AddService<AccessPermissionAuthorizationFilter>());

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Configures bearer-token validation for issuer, audience, lifetime, and signing key.
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:Key"]!
                )
            )
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Registers authorization policies for the application modules.
    foreach (var module in ModuleAccess.AllModules)
    {
        options.AddPolicy(module, policy =>
            policy.AddRequirements(new ModuleAccessRequirement(module)));
    }
    options.AddPolicy("clients", policy =>
        policy.AddRequirements(new ModuleAccessRequirement("clients")));
});

// Request-scoped services share the current operation's dependency-injection scope.
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<AccessPermissionService>();
builder.Services.AddScoped<UserSessionService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AccessPermissionAuthorizationFilter>();
builder.Services.AddSingleton<UndoOperationStore>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
    ModuleAccessAuthorizationHandler>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();

// Keep the local and cloud databases as separate contexts so callers can target the correct store.
builder.Services.AddDbContext<LocalDbContext>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("LocalDatabase")));

builder.Services.AddDbContext<CloudDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AzureDatabase")
));

builder.Services.AddCors(options =>
{
    // Defines the frontend cross-origin request policy.
    options.AddPolicy("FrontendPolicy", policy =>
    {
        // Sets the access requirement applied by this authorization policy.
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Read the App Service home directory used to construct the persistent key path.
// Persist Data Protection keys outside the process so tokens and other protected data survive restarts.
var appServiceHome = Environment.GetEnvironmentVariable("HOME");
// Select a persistent key directory; App Service and local runs use different storage roots.
var dataProtectionKeyPath = !string.IsNullOrWhiteSpace(
    Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"))
    ? Path.Combine(
        appServiceHome ?? throw new InvalidOperationException(
            "The Azure App Service HOME directory is not configured."),
        "ASP.NET",
        "DataProtection-Keys")
    : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ASP.NET",
        "DataProtection-Keys");
Directory.CreateDirectory(dataProtectionKeyPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
builder.Services.AddSingleton<EncryptionService>();

var app = builder.Build();


// Expose the generated API description before the request pipeline handles application traffic.
    app.UseSwagger();
    app.UseSwaggerUI();

// Middleware order is intentional: CORS and HTTPS run before authentication, then identity-aware
// signature access checks precede authorization and controller endpoint dispatch.
app.UseCors("FrontendPolicy");

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseMiddleware<UserSignatureAccessMiddleware>();

app.UseAuthorization();

app.MapControllers();

// Returns the API health and version payload at the root endpoint.
app.MapGet("/", () => Results.Ok(new
{
    status = "online",
    application = "MIRHEMA RBAMS API",
    version = "1.0"
}));

app.Run();
