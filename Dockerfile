# syntax=docker/dockerfile:1

# ------------------------------------------------------------
# Restore and build
# ------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /source

# Copy dependency metadata separately to maximize layer caching.
COPY Vocabula.sln ./

COPY src/Vocabula.Api/Vocabula.Api.csproj \
     src/Vocabula.Api/

COPY src/Vocabula.Core/Vocabula.Core.csproj \
     src/Vocabula.Core/

COPY src/Vocabula.Cli/Vocabula.Cli.csproj \
     src/Vocabula.Cli/

COPY src/Vocabula.Infrastructure/Vocabula.Infrastructure.csproj \
     src/Vocabula.Infrastructure/

COPY tests/Vocabula.Api.Tests/Vocabula.Api.Tests.csproj \
     tests/Vocabula.Api.Tests/

COPY tests/Vocabula.Core.Tests/Vocabula.Core.Tests.csproj \
     tests/Vocabula.Core.Tests/

COPY tests/Vocabula.Infrastructure.Tests/Vocabula.Infrastructure.Tests.csproj \
     tests/Vocabula.Infrastructure.Tests/

RUN dotnet restore Vocabula.sln

# Copy the remaining source after restoring dependencies.
COPY . .

RUN dotnet build Vocabula.sln \
    --configuration Release \
    --no-restore

# ------------------------------------------------------------
# Test
# ------------------------------------------------------------
FROM build AS test

RUN dotnet test Vocabula.sln \
    --configuration Release \
    --no-build

# ------------------------------------------------------------
# Publish only the API
# ------------------------------------------------------------
FROM test AS publish

RUN dotnet publish src/Vocabula.Api/Vocabula.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ------------------------------------------------------------
# Final runtime image
# ------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER app

ENTRYPOINT ["dotnet", "Vocabula.Api.dll"]