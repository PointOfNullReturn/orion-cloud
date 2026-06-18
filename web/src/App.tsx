import { useEffect, useRef, useState, type FormEvent } from "react";
import { fetchWeather, type Units, type WeatherResponse } from "./api/weather";
import { fetchGeocode, type GeocodeResult } from "./api/geocode";
import { fetchReverse } from "./api/reverse";
import { getCurrentPosition, type GeolocationErrorKind } from "./location/geolocation";
import { parseCityQuery, prioritizeByHint } from "./location/cityQuery";
import { decideLocationMode, type PermissionState } from "./location/locationUx";
import { readGeolocationPermission } from "./location/permission";
import { apiErrorMessage, locationNote } from "./ui/messages";
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

function LocateIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
      strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <circle cx="12" cy="12" r="7" />
      <line x1="12" y1="1" x2="12" y2="4" />
      <line x1="12" y1="20" x2="12" y2="23" />
      <line x1="1" y1="12" x2="4" y2="12" />
      <line x1="20" y1="12" x2="23" y2="12" />
      <circle cx="12" cy="12" r="2.5" fill="currentColor" stroke="none" />
    </svg>
  );
}

function SearchIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
      strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <circle cx="11" cy="11" r="7" />
      <line x1="21" y1="21" x2="16.65" y2="16.65" />
    </svg>
  );
}

type Phase = "idle" | "busy" | "ready" | "error";
type Source = "location" | "search";

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
  const [query, setQuery] = useState("");
  const [matches, setMatches] = useState<GeocodeResult[] | null>(null);
  const [flipped, setFlipped] = useState(false);
  const [permission, setPermission] = useState<PermissionState>("unknown");
  const [lastFailure, setLastFailure] = useState<GeolocationErrorKind | null>(null);
  const [searchOpen, setSearchOpen] = useState(false);
  const [source, setSource] = useState<Source | null>(null);
  const [hasLoaded, setHasLoaded] = useState(false);
  const [locating, setLocating] = useState(false);

  // Monotonic id for location requests. A slow reverse-geocode from "Use my
  // location" must not overwrite the label once a newer location supersedes it.
  const requestId = useRef(0);

  // Read the geolocation permission silently at load (Permissions API where
  // available). A known-denied user lands in search-first with no wasted tap;
  // where the API is absent (Safari), this stays "unknown" and we learn from
  // the first attempt instead.
  useEffect(() => {
    let active = true;
    readGeolocationPermission().then((state) => {
      if (active) setPermission(state);
    });
    return () => {
      active = false;
    };
  }, []);

  const busy = phase === "busy";
  const supported = "geolocation" in navigator;
  const mode = decideLocationMode({ supported, permission, lastFailure });
  const note = locationNote(supported, permission, lastFailure);

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
      // Once we've shown a result we stay in the compact chrome — even while
      // searching for a new place — rather than snapping back to the launcher.
      setHasLoaded(true);
    } else {
      setError(apiErrorMessage(result.kind));
    }
  }

  async function useMyLocation() {
    const id = ++requestId.current;
    // Re-acquiring while a result is already on screen: reload "silently" so the
    // card keeps its current contents and swaps in place when the new data
    // arrives, instead of collapsing to a spinner during the (sometimes slow)
    // geolocation lookup. The dimmed controls signal that it's working.
    const silent = phase === "ready" && weather != null;
    if (!silent) setPhase("busy");
    setMessage(null);
    setIsError(false);
    setLastFailure(null);
    setSearchOpen(false);
    setMatches(null);
    setLocating(true);

    try {
      const pos = await getCurrentPosition();
      if (pos.ok) {
        setSource("location");
        // Reverse-geocode in parallel with the weather fetch. Weather paints
        // first (coords as the heading); place name fills in when reverse lands.
        const reverse = fetchReverse(pos.latitude, pos.longitude);
        await loadWeather(pos.latitude, pos.longitude, null, units, silent);
        const place = await reverse;
        // Ignore a stale reverse: a newer request has superseded this one.
        if (place && requestId.current === id) setLocationLabel(placeLabel(place));
      } else {
        // Record the failure; the launcher reacts declaratively — search becomes
        // the hero and `note` explains why. Hard failures (denied/unsupported)
        // drop to search-only; transient ones leave a "try again" affordance.
        setLastFailure(pos.kind);
        if (!silent) setPhase("idle");
      }
    } finally {
      setLocating(false);
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
    setSource("search");
    // Collapse the search once a city is chosen (your flow: search → pick → gone).
    setSearchOpen(false);
    setQuery("");
    loadWeather(m.latitude, m.longitude, placeLabel(m));
  }

  function toggleSearch() {
    setSearchOpen((open) => {
      if (open) {
        // Closing: drop any pending matches/message so it reopens clean.
        setMatches(null);
        setMessage(null);
      }
      return !open;
    });
  }

  const searchForm = (
    <form className="search" onSubmit={search}>
      <input
        type="text"
        placeholder="Search for a city…"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        aria-label="Search for a city"
      />
      <button type="submit" disabled={busy || !query.trim()}>
        Search
      </button>
    </form>
  );

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
              aria-label="Configuration"
              title="Configuration"
            >
              <GearIcon />
            </button>

            {hasLoaded ? (
              <>
                {/* Compact chrome: minimal wordmark + mode controls. The lit
                    icon shows how the current result was obtained. */}
                <div className="result-head">
                  <div className="wordmark-sm">Orion</div>
                  <div className="mode-controls">
                    {mode !== "search-only" && (
                      <button
                        type="button"
                        className={`icon-btn${source === "location" ? " active" : ""}`}
                        onClick={useMyLocation}
                        disabled={busy || locating}
                        aria-label="Get weather by current location"
                        title="Get weather by current location"
                      >
                        <LocateIcon />
                      </button>
                    )}
                    <button
                      type="button"
                      className={`icon-btn${source === "search" ? " active" : ""}`}
                      onClick={toggleSearch}
                      disabled={busy || locating}
                      aria-expanded={searchOpen}
                      aria-label="Get weather by city search"
                      title="Get weather by city search"
                    >
                      <SearchIcon />
                    </button>
                  </div>
                </div>

                {searchOpen && <div className="result-search">{searchForm}</div>}
              </>
            ) : (
              <>
                <header className="masthead">
                  <h1>Orion</h1>
                  <p className="tagline">
                    Weather aggregated from multiple sources.
                  </p>
                </header>

                <div className="launcher">
                  {/* Location leads only when it can; otherwise search is hero. */}
                  {mode === "location-first" && (
                    <>
                      <button
                        className="primary"
                        onClick={useMyLocation}
                        disabled={busy}
                      >
                        Use my location
                      </button>
                      <div className="or">or</div>
                    </>
                  )}

                  {searchForm}

                  {note && <p className="status hint">{note}</p>}

                  {mode === "search-retry" && (
                    <button className="link" onClick={useMyLocation} disabled={busy}>
                      Try my location again
                    </button>
                  )}
                </div>
              </>
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
              <p className="about-tagline">
                Weather aggregated from multiple sources.
              </p>
              {/* Placeholder — real version/build wired from the build later. */}
              <p className="version">v1.0 · build local</p>
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
