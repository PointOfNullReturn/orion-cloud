# Orion

A weather service that aggregates **current conditions from multiple providers**
(Open-Meteo + OpenWeather) behind a single API, with a React widget front end.
The API merges sources on a best-effort basis — if one provider is unavailable
the response still comes back with whatever answered — and also handles city
search. Reverse geocoding (turning your current location into a place name) runs
client-side in the browser via BigDataCloud's free client-side API, per its
fair-use terms.

## Architecture & design choices

Orion is two independent deployables — a .NET API and a React single-page app —
so each ships on its own pipeline.

**Flow:** the browser gets a location (geolocation button or city search), the
SPA calls the API, and the API fans out to the weather providers, merges their
answers, and returns one response. Reverse geocoding (coordinates → place name)
runs in the browser to satisfy BigDataCloud's client-side fair-use terms.

- **API — ASP.NET Core minimal API on .NET 10.** Minimal API keeps the surface
  small and fast with little ceremony; .NET 10 for the current runtime. Weather
  providers sit behind an `IWeatherProvider` interface (Open-Meteo, keyless;
  OpenWeather, keyed) and merge **best-effort** — if one provider is down, the
  response still returns with whatever answered. `HybridCache` gives cache-stampede
  protection, and the API is hardened with per-IP rate limiting, input validation,
  CORS, and a full set of security headers.
- **Front end — React + Vite + TypeScript.** React for the component model, Vite
  for a fast dev loop and a lean production build, and TypeScript to type the API
  contract end to end. The UI is a frosted-glass flip-card widget with light/dark
  themes, unit + theme persistence, and silent auto-refresh.
- **Testing — TDD throughout.** xUnit for the API, Vitest for the front end.
- **Hosting — Azure, scale-to-cost.** Container Apps runs the API with
  scale-to-zero (no traffic, no cost); Static Web Apps serves the SPA on a global
  CDN with free SSL. Secrets live in Key Vault; CI/CD is OIDC-based with no stored
  cloud credentials. See [Deployment](#deployment).

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
| `/health`  | Liveness probe                                               |

Units default to metric; pass `?units=imperial` for Fahrenheit/mph. The response
reports which providers actually answered, and source-specific fields are
nullable, so clients must never assume an optional field is present.

## Running locally

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (for the front end)

### Get the code

```bash
git clone https://github.com/PointOfNullReturn/orion-cloud.git
cd orion-cloud
```

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

## Deployment

Orion is host-agnostic: the API is a container and the front end is a static
build, so it runs on any container host plus any static/CDN host. Azure is simply
the reference deployment — this is not an Azure-only project.

### Self-hosting (any platform)

**API** — build the image and run it, passing config via environment variables:

```bash
docker build -t orion-api .
docker run -p 8080:8080 \
  -e Weather__OpenWeather__ApiKey="<your-openweather-key>" \
  -e Cors__AllowedOrigins__0="https://your-frontend.example" \
  orion-api
```

The container listens on port 8080. The OpenWeather key is optional (without it
the aggregator runs Open-Meteo only); set the CORS origin to wherever the front
end is served so the browser is allowed to call the API.

**Front end** — build the static bundle and serve `web/dist/` from any static
host or CDN:

```bash
cd web
VITE_API_URL="https://your-api.example" npm ci && npm run build
# then deploy the contents of web/dist/
```

`VITE_API_URL` is baked into the bundle at build time, so it must point at your
API *before* you build.

### Configuration reference

| Setting                        | Applies to                 | Purpose                                                                  |
| ------------------------------ | -------------------------- | ------------------------------------------------------------------------ |
| `Weather__OpenWeather__ApiKey` | API (env / secret)         | Enables the OpenWeather source (optional — Open-Meteo works without it)   |
| `Cors__AllowedOrigins__0`      | API (env)                  | Allows your front-end origin to call the API                             |
| `VITE_API_URL`                 | Front end (build-time env) | Points the UI at your API                                                |

### Reference deployment (Azure)

The live instance runs on **Azure Static Web Apps** (front end) and **Azure
Container Apps** (API, scale-to-zero), with images in **Azure Container Registry**
and the OpenWeather key in **Azure Key Vault**. CI/CD is two GitHub Actions
workflows — one per deployable — that build, test, and deploy on push to `main`
via federated OIDC (no stored cloud credentials):

- [`.github/workflows/deploy-api.yml`](.github/workflows/deploy-api.yml) — build & test the API, push the image, roll the Container App
- [`.github/workflows/deploy-web.yml`](.github/workflows/deploy-web.yml) — build & test the front end, deploy to Static Web Apps

Those workflows are the authoritative, runnable deployment steps — no separate runbook to drift out of date.

## License

See [LICENSE](LICENSE).
