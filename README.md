# Vocabula

Storytelling application generating personalized reading content for language learners.

## Current AI spike

A deliberately small .NET console application that:

1. reads one notebook photo from a hardcoded local path;
2. sends it to Mistral vision with a strict JSON schema;
3. extracts vocabulary and grammar without silently guessing uncertain text;
4. sends the structured extraction to Mistral again;
5. generates a short story and reports potentially unfamiliar language;
6. prints and saves the complete result as readable JSON.

## Open in Rider

Open `Vocabula.slnx`. If Rider does not detect the user-local SDK automatically, configure the .NET CLI executable as:

```text
/Users/leo/.dotnet/dotnet
```

The installed SDK is under `/Users/leo/.dotnet` because the Homebrew package required administrator access.

## One-time setup

1. Copy a notebook photo to:

   ```text
   /Users/leo/Development/Projects/Vocabula/input/notebook.jpg
   ```

   JPG, PNG, and WebP are supported. If the extension differs, edit `imagePath` in `src/Vocabula.Spike/Program.cs`.

2. In Rider, create or edit the run configuration for `Vocabula.Spike` and add this environment variable:

   ```text
   MISTRAL_API_KEY=<your key>
   ```

   Do not put the real key in `Program.cs` or commit it.

3. Run `Vocabula.Spike` from Rider.

The result is printed and written to:

```text
/Users/leo/Development/Projects/Vocabula/output/result.json
```

## Hardcoded spike configuration

`src/Vocabula.Spike/Program.cs` intentionally hardcodes:

- input image path;
- output JSON path;
- vision model;
- story model;
- Mistral API base URL.

Only the secret API key remains outside source control.

## Command-line verification

```bash
/Users/leo/.dotnet/dotnet test Vocabula.slnx
/Users/leo/.dotnet/dotnet run --project src/Vocabula.Spike/Vocabula.Spike.csproj
```

The live run makes two model requests: one extraction request and one story-generation request.
