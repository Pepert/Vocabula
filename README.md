# Vocabula

Storytelling application generating personalized reading content for language learners.

## ASP.NET Core API

A local ASP.NET Core Web API that provides:

- `POST /api/knowledge/extract` - Extract structured vocabulary and grammar from a notebook image
- `POST /api/stories/generate` - Generate a story from extracted knowledge
- `GET /health` - Health check endpoint

### Open in Rider

Open `Vocabula.sln`. If Rider does not detect the SDK automatically, configure the .NET CLI executable as:

```text
/Users/leo/.dotnet/dotnet
```

### Run the API

1. In Rider, create or edit the run configuration for `Vocabula.Api` and add this environment variable:

   ```text
   MISTRAL_API_KEY=<your key>
   ```

   Do not put the real key in source code.

2. Run `Vocabula.Api` from Rider.

The API starts on `http://localhost:5000` with Swagger UI available at `http://localhost:5000/swagger`.

### Command-line usage

Alternatively, use the CLI application `Vocabula.Cli`:

1. Copy a notebook photo to:

   ```text
   /Users/leo/Development/Projects/Vocabula/input/notebook.jpeg
   ```

   JPG, PNG, and WebP are supported.

2. In Rider, create or edit the run configuration for `Vocabula.Cli` and add this environment variable:

   ```text
   MISTRAL_API_KEY=<your key>
   ```

3. Run `Vocabula.Cli` from Rider.

The result is printed and written to:

```text
/Users/leo/Development/Projects/Vocabula/output/result.json
```

## Architecture

- `Vocabula.Core` - Domain models and AI services (no ASP.NET dependencies)
- `Vocabula.Cli` - Console application
- `Vocabula.Api` - ASP.NET Core Web API
