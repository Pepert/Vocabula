using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Vocabula.Api.Authorization;
using Vocabula.Core.AI;
using Vocabula.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Load local development settings from the repository's .env file when running
// outside Docker Compose (which injects the same variables as environment variables).
// Keys use the standard __ separator, e.g. AUTHENTICATION__COGNITO__AUTHORITY.
if (builder.Environment.IsDevelopment())
{
    var directory = new DirectoryInfo(builder.Environment.ContentRootPath);
    while (directory != null && !File.Exists(Path.Combine(directory.FullName, ".env")))
    {
        directory = directory.Parent;
    }

    if (directory != null)
    {
        var settings = File.ReadAllLines(Path.Combine(directory.FullName, ".env"))
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#'))
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(
                parts => parts[0].Trim().Replace("__", ":"),
                parts => (string?)parts[1].Trim());

        builder.Configuration.AddInMemoryCollection(settings);
    }
}

builder.Services.AddSingleton<IMistralClient>(_ =>
{
    var apiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        throw new InvalidOperationException(
            "MISTRAL_API_KEY environment variable is required but not set.");
    }

    return new MistralClient(
        new HttpClient { BaseAddress = new Uri("https://api.mistral.ai/"), Timeout = TimeSpan.FromMinutes(2) },
        apiKey);
});

builder.Services.AddSingleton<INotebookKnowledgeExtractor>(_ =>
    new NotebookKnowledgeExtractor(
        _.GetRequiredService<IMistralClient>(),
        "mistral-ocr-latest"));

builder.Services.AddSingleton<IStoryGenerator>(_ =>
    new StoryGenerator(
        _.GetRequiredService<IMistralClient>(),
        "mistral-small-latest"));

builder.Services.AddDbContext<VocabulaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("VocabulaDb")));

// Configure Cognito authentication
var cognitoAuthority = builder.Configuration["Authentication:Cognito:Authority"];
var cognitoAppClientId = builder.Configuration["Authentication:Cognito:AppClientId"];

if (string.IsNullOrWhiteSpace(cognitoAuthority) || string.IsNullOrWhiteSpace(cognitoAppClientId))
{
    throw new InvalidOperationException(
        "Cognito authentication configuration is missing. " +
        "Please set Authentication:Cognito:Authority and Authentication:Cognito:AppClientId.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = cognitoAuthority;
        // Keep Cognito claim names (sub, iss, client_id, token_use) unmapped
        // so authorization code and CurrentAppUser can find them by their JWT names.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = cognitoAuthority,
            ValidateAudience = false,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var clientId = context.Principal?.FindFirst("client_id")?.Value;
                if (clientId != cognitoAppClientId)
                {
                    context.Fail("Invalid client_id claim");
                }

                var tokenUse = context.Principal?.FindFirst("token_use")?.Value;
                if (tokenUse != "access")
                {
                    context.Fail("Token is not an access token");
                }

                var sub = context.Principal?.FindFirst("sub")?.Value;
                if (string.IsNullOrWhiteSpace(sub))
                {
                    context.Fail("Missing or empty sub claim");
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentAppUser, CurrentAppUser>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Vocabula API", Version = "v1" });

    // Swagger Bearer auth configuration for Authorize button
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Cognito access token"
    });

    // Add global security requirement. The scheme reference needs the OpenApiDocument
    // so that the requirement serializes as {"Bearer": []}; without it the requirement
    // is emitted empty and Swagger UI never attaches the token to requests.
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Vocabula API v1");
});

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Logger.LogInformation("Swagger UI is available at /swagger");

app.Run();
