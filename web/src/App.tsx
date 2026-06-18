import { useRef, useState, type FormEvent } from "react";
import { fetchWeather, type Units, type WeatherResponse } from "./api/weather";
import { fetchGeocode, type GeocodeResult } from "./api/geocode";
import { fetchReverse } from "./api/reverse";
import { getCurrentPosition } from "./location/geolocation";
import { parseCityQuery, prioritizeByHint } from "./location/cityQuery";
import { apiErrorMessage, geolocationMessage } from "./ui/messages";
import { WeatherCard } from "./ui/WeatherCard";
import { ThemeToggle } from "./ui/ThemeToggle";
import "./App.css";

function GearIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
      strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <circle cx="12" cy="12" r="3" />
      <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
    </svg>
  );
}

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
  const [flipped, setFlipped] = useState(false);

  // Monotonic id for location requests. A slow reverse-geocode from "Use my
  // location" must not overwrite the label once a newer location supersedes it.
  const requestId = useRef(0);

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
    silent = false,
  ) {
    // Silent reload (used for a units change): keep the current weather on
    // screen and the card on whatever face it's showing — don't drop to the
    // "busy" state, which would unmount the card, collapse its height, and make
    // the 3D-rotated widget appear to flip back to the front.
    if (!silent) {
      setPhase("busy");
      setMatches(null);
    }
    setMessage(null);
    setIsError(false);

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
    const id = ++requestId.current;
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
      // Ignore a stale reverse: a newer location request has superseded this one.
      if (place && requestId.current === id) setLocationLabel(placeLabel(place));
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
      loadWeather(coords.lat, coords.lon, locationLabel, next, true);
    }
  }

  function selectMatch(m: GeocodeResult) {
    // Picking a city supersedes any in-flight reverse from "Use my location".
    requestId.current++;
    loadWeather(m.latitude, m.longitude, placeLabel(m));
  }

  return (
    <main className="app">
      <div className="widget">
        <div className={`flip${flipped ? " flipped" : ""}`}>
          {/* Front — the whole app. inert when flipped away. */}
          <section className="face card" inert={flipped || undefined}>
            <div className="theme-corner">
              <ThemeToggle />
            </div>

            <button
              className="cog"
              type="button"
              onClick={() => setFlipped(true)}
              aria-label="Settings"
              title="Settings"
            >
              <GearIcon />
            </button>

            <header className="masthead">
              <h1>Orion</h1>
              <p className="tagline">
                Weather aggregated from multiple sources.
              </p>
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
                    <button onClick={() => selectMatch(m)}>{placeLabel(m)}</button>
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
          </section>

          {/* Back — settings. inert until flipped into view. */}
          <div className="face card-back" inert={!flipped || undefined}>
            <header className="about">
              <h2 className="wordmark">Orion</h2>
              {/* Placeholder — real version/build wired from the build later. */}
              <p className="version">v1.0 · build local</p>
              <p className="about-tagline">
                Weather aggregated from multiple sources.
              </p>
            </header>

            <div className="setting">
              <span className="setting-label">Units</span>
              <div className="unit-options" role="radiogroup" aria-label="Units">
                <button
                  type="button"
                  role="radio"
                  aria-checked={units === "metric"}
                  className={`unit-option${units === "metric" ? " active" : ""}`}
                  onClick={() => changeUnits("metric")}
                >
                  <span className="unit-name">Metric</span>
                  <span className="unit-desc">
                    Celsius and km/h. Used in most of the world.
                  </span>
                </button>
                <button
                  type="button"
                  role="radio"
                  aria-checked={units === "imperial"}
                  className={`unit-option${units === "imperial" ? " active" : ""}`}
                  onClick={() => changeUnits("imperial")}
                >
                  <span className="unit-name">Imperial</span>
                  <span className="unit-desc">
                    Fahrenheit and mph. Common in the United States.
                  </span>
                </button>
              </div>
            </div>

            <button
              className="primary done"
              type="button"
              onClick={() => setFlipped(false)}
            >
              Done
            </button>
          </div>
        </div>
      </div>
    </main>
  );
}

export default App;
