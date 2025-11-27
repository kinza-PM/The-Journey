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

var pgHost = Environment.GetEnvironmentVariable("PG_HOST") ?? throw new InvalidOperationException("PG_HOST environment variable is required");
var pgUser = Environment.GetEnvironmentVariable("PG_USER") ?? throw new InvalidOperationException("PG_USER environment variable is required");
var pgPassword = Environment.GetEnvironmentVariable("PG_PASSWORD") ?? throw new InvalidOperationException("PG_PASSWORD environment variable is required");
var pgDb = Environment.GetEnvironmentVariable("PG_DB") ?? throw new InvalidOperationException("PG_DB environment variable is required");

var connectionString = $"Host={pgHost};Username={pgUser};Password={pgPassword};Database={pgDb};SSL Mode=Require;Trust Server Certificate=true";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new InvalidOperationException("JWT_SECRET environment variable is required");
var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? throw new InvalidOperationException("JWT_ISSUER environment variable is required");
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? throw new InvalidOperationException("JWT_AUDIENCE environment variable is required");

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
var allowedOrigins = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS")?.Split(',') 
    ?? new[] { "*" }; // Default to allow all in development

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
    var apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") 
        ?? "https://thejourney-api-dev-b0hscbf3eqchhsak.centralus-01.azurewebsites.net";
    
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "TheJourney API", 
        Version = "v1",
        Description = "TheJourney API - Production Environment",
        Contact = new OpenApiContact
        {
            Name = "TheJourney API Support"
        }
    });
    
    // Add production server URL
    c.AddServer(new OpenApiServer
    {
        Url = apiBaseUrl,
        Description = "Production Server"
    });
    
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
});

var app = builder.Build();

// Enable Swagger in all environments (useful for API documentation)
// To disable in production, set ENABLE_SWAGGER=false environment variable
var enableSwagger = Environment.GetEnvironmentVariable("ENABLE_SWAGGER") != "false";
if (enableSwagger || app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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
        
        var seedEmail = Environment.GetEnvironmentVariable("SEED_ADMIN_EMAIL");
        var seedPassword = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD");
        
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
