import { useState, type FormEvent } from "react";
import { fetchWeather, type Units, type WeatherResponse } from "./api/weather";
import { fetchGeocode, type GeocodeResult } from "./api/geocode";
import { fetchReverse } from "./api/reverse";
import { getCurrentPosition } from "./location/geolocation";
import { parseCityQuery, prioritizeByHint } from "./location/cityQuery";
import { apiErrorMessage, geolocationMessage } from "./ui/messages";
import { WeatherCard } from "./ui/WeatherCard";
import "./App.css";

type Phase = "idle" | "busy" | "ready" | "error";

function placeLabel(r: GeocodeResult): string {
  // Use the country code, not the name — BigDataCloud returns verbose official
  // names ("United States of America (the)"); the code is clean and consistent
  // across both providers. Region already disambiguates within a country.
  return [r.name, r.region, r.countryCode].filter(Boolean).join(", ");
}

function App() {
  const [units, setUnits] = useState<Units>("metric");
  const [phase, setPhase] = useState<Phase>("idle");
  const [weather, setWeather] = useState<WeatherResponse | null>(null);
  const [locationLabel, setLocationLabel] = useState<string | null>(null);
  const [coords, setCoords] = useState<{ lat: number; lon: number } | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [isError, setIsError] = useState(false);
  const [showSearch, setShowSearch] = useState(false);
  const [query, setQuery] = useState("");
  const [matches, setMatches] = useState<GeocodeResult[] | null>(null);

  const busy = phase === "busy";

  function setError(text: string) {
    setMessage(text);
    setIsError(true);
    setPhase("error");
  }

  async function loadWeather(
    lat: number,
    lon: number,
    label: string | null,
    nextUnits: Units = units,
  ) {
    setPhase("busy");
    setMessage(null);
    setIsError(false);
    setMatches(null);

    const result = await fetchWeather(lat, lon, nextUnits);
    if (result.ok) {
      setWeather(result.data);
      setCoords({ lat, lon });
      setLocationLabel(label);
      setPhase("ready");
    } else {
      setError(apiErrorMessage(result.kind));
    }
  }

  async function useMyLocation() {
    setPhase("busy");
    setMessage(null);
    setIsError(false);

    const pos = await getCurrentPosition();
    if (pos.ok) {
      // Reverse-geocode in parallel with the weather fetch. Weather paints first
      // (coords as the heading); the place name fills in when reverse resolves.
      const reverse = fetchReverse(pos.latitude, pos.longitude);
      await loadWeather(pos.latitude, pos.longitude, null);
      const place = await reverse;
      if (place) setLocationLabel(placeLabel(place));
    } else {
      // Geolocation failed — reveal the manual fallback and explain why.
      setShowSearch(true);
      setMessage(geolocationMessage(pos.kind));
      setIsError(false);
      setPhase("idle");
    }
  }

  async function search(e: FormEvent) {
    e.preventDefault();
    const { name, hint } = parseCityQuery(query);
    if (!name) return;

    setPhase("busy");
    setMessage(null);
    setIsError(false);
    setMatches(null);

    const result = await fetchGeocode(name);
    if (!result.ok) {
      setError(apiErrorMessage(result.kind));
      return;
    }
    if (result.results.length === 0) {
      setMessage(`No places found for "${name}".`);
      setPhase("idle");
      return;
    }
    setMatches(prioritizeByHint(result.results, hint));
    setPhase("idle");
  }

  function changeUnits(next: Units) {
    if (next === units) return;
    setUnits(next);
    if (coords) {
      loadWeather(coords.lat, coords.lon, locationLabel, next);
    }
  }

  return (
    <main className="app">
      <header className="masthead">
        <h1>Orion</h1>
        <p className="tagline">Current weather, aggregated from multiple sources.</p>
      </header>

      <div className="controls">
        <button className="primary" onClick={useMyLocation} disabled={busy}>
          Use my location
        </button>
        <button
          className="link"
          onClick={() => setShowSearch((s) => !s)}
          disabled={busy}
        >
          Search by city
        </button>

        <div className="units" role="group" aria-label="Units">
          <button
            className={units === "metric" ? "active" : ""}
            onClick={() => changeUnits("metric")}
            disabled={busy}
          >
            °C
          </button>
          <button
            className={units === "imperial" ? "active" : ""}
            onClick={() => changeUnits("imperial")}
            disabled={busy}
          >
            °F
          </button>
        </div>
      </div>

      {showSearch && (
        <form className="search" onSubmit={search}>
          <input
            type="text"
            placeholder="City name…"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            aria-label="City name"
          />
          <button type="submit" disabled={busy || !query.trim()}>
            Search
          </button>
        </form>
      )}

      {matches && matches.length > 0 && (
        <ul className="matches">
          {matches.map((m) => (
            <li key={`${m.latitude},${m.longitude}`}>
              <button onClick={() => loadWeather(m.latitude, m.longitude, placeLabel(m))}>
                {placeLabel(m)}
              </button>
            </li>
          ))}
        </ul>
      )}

      {busy && <p className="status">Loading…</p>}
      {message && (
        <p className={isError ? "status error" : "status hint"}>{message}</p>
      )}

      {phase === "ready" && weather && (
        <WeatherCard weather={weather} locationLabel={locationLabel} />
      )}
    </main>
  );
}

export default App;
