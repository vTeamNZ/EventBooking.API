using EventBooking.API;
using EventBooking.API.Data;
using EventBooking.API.Models;
using EventBooking.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);

// Configure for IIS integration
builder.WebHost.UseIISIntegration();

// Only configure URLs when not running under IIS in development
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://localhost:5000");
}

// Force configuration to be rebuilt
builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

// Debug information
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"Config files loaded:");
Console.WriteLine($" - {Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json")}");
Console.WriteLine($" - {Path.Combine(Directory.GetCurrentDirectory(), $"appsettings.{builder.Environment.EnvironmentName}.json")}");
Console.WriteLine($"Connection String: {builder.Configuration.GetConnectionString("DefaultConnection")}");

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddEventLog();

// Add File logging
var logsPath = Path.Combine(builder.Environment.ContentRootPath, "logs");
builder.Logging.AddFile(Path.Combine(logsPath, "app-{Date}.log"));

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", builder =>
    {
        builder.WithOrigins("https://kiwilanka.co.nz", "http://localhost:3000", "https://thelankanspace.co.nz")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});

// REMOVED: ReservationCleanupService registration - service disabled as duplicate system removed
// builder.Services.AddHostedService<ReservationCleanupService>();

// Configure Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Event Booking API",
        Version = "v1",
        Description = "API for Event Booking System"
    });

    // Add JWT bearer security scheme
    var jwtSecurityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter 'Bearer {your token}'",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Id = "Bearer",
            Type = ReferenceType.SecurityScheme
        }
    };

    c.AddSecurityDefinition("Bearer", jwtSecurityScheme);

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtSecurityScheme, Array.Empty<string>() }
    });

    // Optional but useful to include all endpoints regardless of auth
    c.DocInclusionPredicate((docName, apiDesc) => true);
});

// Configure Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Configure Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
}
Console.WriteLine($"Using connection string from config: {connectionString}");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
        sqlOptions.CommandTimeout(300); // 5 minutes command timeout
    });
    options.EnableSensitiveDataLogging(false); // Disable noisy EF logging for production
    if (builder.Environment.IsDevelopment())
    {
        options.LogTo(Console.WriteLine, LogLevel.Warning); // Only warnings and errors
    }
});

// Add Event Status Service
builder.Services.AddScoped<IEventStatusService, EventStatusService>();

// Add Image Service
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<ISeatCreationService, SeatCreationService>();
builder.Services.AddScoped<ISeatAllocationService, SeatAllocationService>();

// Add Ticket Availability Service
builder.Services.AddScoped<ITicketAvailabilityService, TicketAvailabilityService>();

// Add Processing Fee Service
builder.Services.AddScoped<IProcessingFeeService, ProcessingFeeService>();

// Configure Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => 
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 6;
    // Make sure Identity uses our database
    options.Stores.MaxLengthForKeys = 128;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Configure JWT Authentication (only for organizer endpoints)
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("Authentication failed: {Message}", context.Exception.Message);
            
            if (context.Exception is SecurityTokenMalformedException)
            {
                logger.LogError("Malformed JWT token received: {Token}", context.Request.Headers.Authorization.FirstOrDefault());
            }
            
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            // Token validation successful - no logging needed for production
            return Task.CompletedTask;
        }
    };
    
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://kiwilanka.co.nz",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "https://kiwilanka.co.nz",
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "ThisIsASuperUltraSecretJWTKeyWithMinimum32Bytes")),
        ClockSkew = TimeSpan.Zero
    };
});

// Add Authorization
builder.Services.AddAuthorization();

// Add consolidated services for industry-standard architecture
builder.Services.AddScoped<IQRTicketService, QRTicketService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IBookingConfirmationService, BookingConfirmationService>();

var app = builder.Build();

// Configure CORS middleware
app.UseCors("AllowFrontend");

// Global error handling
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception occurred while processing request to {Path}", context.Request.Path);

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var error = new
        {
            Message = "An error occurred while processing your request.",
            Details = app.Environment.IsDevelopment() || app.Environment.IsStaging() ? ex.ToString() : null,
            Path = context.Request.Path,
            Method = context.Request.Method,
            Timestamp = DateTime.UtcNow
        };

        await context.Response.WriteAsJsonAsync(error);
    }
});

// Add custom middleware to handle JWT token issues
app.Use(async (context, next) =>
{
    try
    {
        // Check if there's an Authorization header
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();
            
            // Basic validation to check if the token has the correct format
            if (!string.IsNullOrEmpty(token) && !token.Contains('.'))
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("Malformed JWT token received - missing dots: {Token}", token);
                
                // Remove the malformed token to prevent authentication errors
                context.Request.Headers.Remove("Authorization");
                
                // Continue without the invalid token - this will result in a 401 for protected endpoints
                await next();
                return;
            }
            
            // Additional validation for minimum token structure
            if (!string.IsNullOrEmpty(token))
            {
                var parts = token.Split('.');
                if (parts.Length != 3)
                {
                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogWarning("Malformed JWT token received - incorrect number of parts: {PartsCount}, Token: {Token}", 
                        parts.Length, token);
                    
                    // Remove the malformed token
                    context.Request.Headers.Remove("Authorization");
                }
            }
        }
        
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error in JWT preprocessing middleware");
        await next();
    }
});

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || app.Environment.IsStaging() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/api/swagger/v1/swagger.json", "Event Booking API v1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
    });
}

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed the database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    
    // Ensure database is created
    context.Database.EnsureCreated();
    
    // Seed initial data
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);
}

app.Run();
