using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Vocabula.Api.Authorization;
using Vocabula.Core.AI;
using Vocabula.Core.Models;
using Vocabula.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

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

    // Add global security requirement using dictionary
    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer")] = new List<string>()
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
