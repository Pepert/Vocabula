using Microsoft.EntityFrameworkCore;
using Npgsql;
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
        "mistral-small-latest"));

builder.Services.AddSingleton<IStoryGenerator>(_ =>
    new StoryGenerator(
        _.GetRequiredService<IMistralClient>(),
        "mistral-small-latest"));

builder.Services.AddDbContext<VocabulaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("VocabulaDb")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Vocabula API", Version = "v1" });
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
app.MapControllers();

app.Logger.LogInformation("Swagger UI is available at /swagger");

app.Run();
