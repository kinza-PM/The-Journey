using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TheJourney.Api.Infrastructure.Database;
using TheJourney.Api.Modules.Admin.Auth.Models;
using TheJourney.Api.Modules.Admin.Auth.Services;
using TheJourney.Api.Modules.Admin.CareerFramework.Services;
using TheJourney.Api.Modules.Mobile.Auth.Notifications;
using TheJourney.Api.Modules.Mobile.Auth.Services;
using TheJourney.Api.Modules.Mobile.Assessment.Services;
using TheJourney.Api.Modules.Mobile.Profile.Services;

var builder = WebApplication.CreateBuilder(args);

// Read from configuration (appsettings.json) first, then fall back to environment variables
var pgHost = builder.Configuration["Database:Host"] ?? Environment.GetEnvironmentVariable("PG_HOST") ?? throw new InvalidOperationException("PG_HOST environment variable or Database:Host configuration is required");
var pgUser = builder.Configuration["Database:User"] ?? Environment.GetEnvironmentVariable("PG_USER") ?? throw new InvalidOperationException("PG_USER environment variable or Database:User configuration is required");
var pgPassword = builder.Configuration["Database:Password"] ?? Environment.GetEnvironmentVariable("PG_PASSWORD") ?? throw new InvalidOperationException("PG_PASSWORD environment variable or Database:Password configuration is required");
var pgDb = builder.Configuration["Database:Name"] ?? Environment.GetEnvironmentVariable("PG_DB") ?? throw new InvalidOperationException("PG_DB environment variable or Database:Name configuration is required");

var connectionString = $"Host={pgHost};Username={pgUser};Password={pgPassword};Database={pgDb};SSL Mode=Require;Trust Server Certificate=true";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var jwtSecret = builder.Configuration["JWT:Secret"] ?? Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new InvalidOperationException("JWT_SECRET environment variable or JWT:Secret configuration is required");
var jwtIssuer = builder.Configuration["JWT:Issuer"] ?? Environment.GetEnvironmentVariable("JWT_ISSUER") ?? throw new InvalidOperationException("JWT_ISSUER environment variable or JWT:Issuer configuration is required");
var jwtAudience = builder.Configuration["JWT:Audience"] ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? throw new InvalidOperationException("JWT_AUDIENCE environment variable or JWT:Audience configuration is required");

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "JWT_OR_SESSION";
    options.DefaultChallengeScheme = "JWT_OR_SESSION";
    options.DefaultScheme = "JWT_OR_SESSION";
})
.AddJwtBearer("JWT", options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
})
.AddCookie("Session", options =>
{
    options.Cookie.Name = "TheJourney.Session";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
})
.AddPolicyScheme("JWT_OR_SESSION", "JWT_OR_SESSION", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        string? authorization = context.Request.Headers.Authorization;
        if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer "))
        {
            return "JWT";
        }
        return "Session";
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly", policy => policy.RequireRole("SuperAdmin"));
    options.AddPolicy("AdminAccess", policy => policy.RequireRole("SuperAdmin", "Admin"));
    options.AddPolicy("RequireRole", policy => 
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
        {
            var roleClaim = context.User.FindFirst(ClaimTypes.Role);
            var sessionRole = context.User.FindFirst("Role");
            return roleClaim != null && !string.IsNullOrWhiteSpace(roleClaim.Value) ||
                   sessionRole != null && !string.IsNullOrWhiteSpace(sessionRole.Value);
        });
    });
});

builder.Services.AddHttpContextAccessor();
// HttpClient factory used by LinkedInController and other services
builder.Services.AddHttpClient();
// In-memory cache for storing OAuth state
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailSender, MailtrapSmtpEmailSender>();
builder.Services.AddScoped<IMobileAuthService, MobileAuthService>();
builder.Services.AddScoped<ICareerFrameworkService, CareerFrameworkService>();
builder.Services.AddScoped<IFitScoreCalculator, FitScoreCalculator>();
builder.Services.AddScoped<IAssessmentService, AssessmentService>();
builder.Services.AddScoped<IResumeExtractionService, ResumeExtractionService>();
builder.Services.AddScoped<IProfileService, ProfileService>();

// Configure CORS for mobile apps
var corsConfig = builder.Configuration["CORS:AllowedOrigins"] ?? Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
var allowedOrigins = corsConfig?.Split(',') ?? new[] { "*" }; // Default to allow all in development

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMobileApps", policy =>
    {
        if (allowedOrigins.Contains("*"))
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var isDevelopment = builder.Environment.IsDevelopment();
    
    // In development, always use localhost:5097 (matches launchSettings.json)
    // In production, use the configured API base URL, environment variable, or Azure Web App URL as fallback
    var defaultServerUrl = isDevelopment 
        ? "http://localhost:5097" 
        : (builder.Configuration["API:BaseUrl"] 
            ?? Environment.GetEnvironmentVariable("API_BASE_URL") 
            ?? "https://thejourney-api-dev-b0hscbf3eqchhsak.centralus-01.azurewebsites.net");
    
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "TheJourney API", 
        Version = "v1",
        Description = isDevelopment ? "TheJourney API - Development Environment" : "TheJourney API - Production Environment",
        Contact = new OpenApiContact
        {
            Name = "TheJourney API Support"
        }
    });
    
    // Add the default server URL (this will be the primary server Swagger uses)
    c.AddServer(new OpenApiServer
    {
        Url = defaultServerUrl,
        Description = isDevelopment ? "Local Development Server (Port 5097)" : "Production Server"
    });
    
    // In production, also add production server as an option if configured differently
    if (!isDevelopment)
    {
        var productionUrl = builder.Configuration["API:BaseUrl"] ?? Environment.GetEnvironmentVariable("API_BASE_URL");
        if (!string.IsNullOrEmpty(productionUrl) && productionUrl != defaultServerUrl)
        {
            c.AddServer(new OpenApiServer
            {
                Url = productionUrl,
                Description = "Production Server"
            });
        }
    }
    
    // Handle conflicting actions by selecting the first one
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
    
    // Use fully qualified names for schema IDs to avoid conflicts between types with the same name
    c.CustomSchemaIds(type => type.FullName);
    
    // Ignore obsolete actions/properties to prevent Swagger generation errors
    c.IgnoreObsoleteActions();
    c.IgnoreObsoleteProperties();
    
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
    
    // Ignore errors in Swagger generation to prevent crashes
    c.IgnoreObsoleteActions();
    c.IgnoreObsoleteProperties();
});

var app = builder.Build();

// Enable Swagger in all environments (useful for API documentation)
// To disable in production, set ENABLE_SWAGGER=false environment variable or Swagger:Enabled=false in config
var swaggerConfig = builder.Configuration["Swagger:Enabled"];
var enableSwagger = swaggerConfig == null 
    ? Environment.GetEnvironmentVariable("ENABLE_SWAGGER") != "false" 
    : bool.Parse(swaggerConfig);
if (enableSwagger || app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        // Disable caching in development
        if (app.Environment.IsDevelopment())
        {
            c.ConfigObject.PersistAuthorization = false;
        }
    });
}

app.UseCors("AllowMobileApps");
app.UseHttpsRedirection();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    
    try
    {
        context.Database.Migrate();
        
        var seedEmail = builder.Configuration["Seed:AdminEmail"] ?? Environment.GetEnvironmentVariable("SEED_ADMIN_EMAIL");
        var seedPassword = builder.Configuration["Seed:AdminPassword"] ?? Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD");
        
        if (!string.IsNullOrEmpty(seedEmail) && !string.IsNullOrEmpty(seedPassword))
        {
            var existingAdmin = await context.Admins.FirstOrDefaultAsync(a => a.Email == seedEmail.ToLower());
            
            if (existingAdmin == null)
            {
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(seedPassword);
                
                var superAdmin = new Admin
                {
                    Email = seedEmail.ToLower(),
                    PasswordHash = passwordHash,
                    Role = "SuperAdmin",
                    FailedLoginAttempts = 0,
                    IsLocked = false,
                    CreatedAt = DateTime.UtcNow
                };
                
                context.Admins.Add(superAdmin);
                await context.SaveChangesAsync();
                
                Console.WriteLine($"SuperAdmin seeded successfully: {seedEmail}");
            }
            else
            {
                Console.WriteLine($"SuperAdmin already exists: {seedEmail}");
            }
        }
        else
        {
            Console.WriteLine("Warning: SEED_ADMIN_EMAIL and SEED_ADMIN_PASSWORD not set. Skipping seed.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error during migration or seeding: {ex.Message}");
        throw;
    }
}

app.Run();
