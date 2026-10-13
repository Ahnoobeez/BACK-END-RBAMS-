using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
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
    foreach (var module in ModuleAccess.AllModules)
    {
        options.AddPolicy(module, policy =>
            policy.RequireAssertion(ctx =>
            {
                var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value ?? "";
                var dept = ctx.User.FindFirst("Department")?.Value ?? "";
                return ModuleAccess.CanAccess(role, dept, module);
            }));
        // Job orders: needed by Marketing, Technical, Production
        options.AddPolicy("clients", policy =>
            policy.RequireAssertion(ctx =>
            {
                var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value ?? "";
                var dept = ctx.User.FindFirst("Department")?.Value ?? "";
                return new[] { "marketing", "technical", "production", "admin"}
                    .Any(m => ModuleAccess.CanAccess(role, dept, m));
            }));
    }
});

builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<WorkflowService>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<LocalDbContext>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("LocalDatabase")));

builder.Services.AddDbContext<CloudDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AzureDatabase")
));

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var appServiceHome = Environment.GetEnvironmentVariable("HOME");
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
builder.Services.AddScoped<EncryptionService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("FrontendPolicy");

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    status = "online",
    application = "MIRHEMA RBAMS API",
    version = "1.0"
}));

app.Run();
