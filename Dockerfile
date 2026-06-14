# Build stage: SDK image has the compiler + dotnet CLI for `publish`
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy just the csproj first so `dotnet restore` caches independently of source.
# Restore only re-runs when dependencies change, not on every .cs edit.
COPY src/Orion.Api/Orion.Api.csproj Orion.Api/
RUN dotnet restore Orion.Api/Orion.Api.csproj

# Now copy the rest of the source — changes here won't bust the restore cache above.
COPY src/Orion.Api/ Orion.Api/

# Publish a Release build to /app/publish — the runtime stage will copy from this path.
RUN dotnet publish Orion.Api/Orion.Api.csproj -c Release -o /app/publish

# Runtime stage: smaller image with just the ASP.NET runtime — no compiler, no source.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Pull just the published output from the build stage — SDK and source stay behind.
COPY --from=build /app/publish .

# Bind Kestrel to all interfaces on 8080. The `+` wildcard is necessary in containers.
ENV ASPNETCORE_URLS=http://+:8080

# Documentation only — actual host-port mapping happens at `docker run -p`.
EXPOSE 8080

# Launch the published app. JSON array form so dotnet runs as PID 1 and receives SIGTERM directly.
ENTRYPOINT [ "dotnet", "Orion.Api.dll" ]