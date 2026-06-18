# Orion

A weather service that aggregates **current conditions from multiple providers**
(Open-Meteo + OpenWeather) behind a single API, with a React widget front end.
The API merges sources on a best-effort basis — if one provider is unavailable
the response still comes back with whatever answered — and also handles city
search and reverse geocoding so the UI only ever talks to Orion.

> **Status:** work in progress. Live URL and full API reference coming soon.

## Tech stack

- **API:** ASP.NET Core minimal API (.NET 10)
- **Front end:** React + Vite + TypeScript
- **Hosting:** Azure Container Apps (API); Azure Static Web Apps (UI, planned)

## Repository layout

| Path            | What it is                          |
| --------------- | ----------------------------------- |
| `src/Orion.Api` | The .NET API                        |
| `tests`         | xUnit test suite                    |
| `web`           | React + Vite + TypeScript front end |

## API endpoints

| Endpoint   | Purpose                                                       |
| ---------- | ------------------------------------------------------------ |
| `/weather` | Aggregated current weather: `?lat&lon&units[&provider]`      |
| `/geocode` | City search → coordinates: `?q&count`                        |
| `/reverse` | Coordinates → place name: `?lat&lon`                         |
| `/health`  | Liveness probe                                               |

Units default to metric; pass `?units=imperial` for Fahrenheit/mph. The response
reports which providers actually answered, and source-specific fields are
nullable, so clients must never assume an optional field is present.

## Running locally

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (for the front end)

### 1. The API

```bash
dotnet run --project src/Orion.Api
```

This runs in the **Development** environment on <http://localhost:5204> (its
CORS policy allows the Vite dev origin). Out of the box the OpenWeather key is
empty, so the aggregator runs **Open-Meteo only** — a missing key isn't an
error, just one fewer source. To get *both* providers, add your key via .NET
User Secrets (below).

#### OpenWeather API key via .NET User Secrets

The key must never enter a tracked file. .NET User Secrets keeps it in a plain
JSON file in your home directory — *outside* the repo — that the config system
layers on top of `appsettings.json` **only in Development**. In production the
provider is never registered, and the key comes from the Container App's
environment variable → Azure Key Vault instead. Same code, no `#if DEBUG`.

The project's `UserSecretsId` is already committed in `Orion.Api.csproj` (it's a
label for the project, not a secret), so you skip `init` and go straight to
setting your own key:

```bash
cd src/Orion.Api
dotnet user-secrets set "Weather:OpenWeather:ApiKey" "<your-openweather-key>"
dotnet user-secrets list   # verify
```

Note the **colon** separator for the CLI. Environment variables use `__`
instead (`Weather__OpenWeather__ApiKey`) because shells disallow colons — same
config path, different escaping. The env-var form is what production uses. Grab
a free key at <https://openweathermap.org/api>.

### 2. The front end

```bash
cd web
npm install
npm run dev
```

The dev server runs at <http://localhost:5173> and talks to the API at
`http://localhost:5204` (configured in `web/.env.development`). Start the API
first so requests have something to hit.

## Tests

```bash
dotnet test          # API (xUnit)
cd web && npm test   # front end (Vitest)
```

## License

See [LICENSE](LICENSE).
