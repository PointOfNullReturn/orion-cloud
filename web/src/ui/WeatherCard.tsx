import type { Provider, WeatherResponse } from "../api/weather";

const PROVIDER_LABELS: Record<Provider, string> = {
  openmeteo: "Open-Meteo",
  openweather: "OpenWeather",
};

const COMPASS = [
  "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
  "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW",
];

function degToCompass(deg: number): string {
  return COMPASS[Math.round(deg / 22.5) % 16];
}

export function WeatherCard({
  weather,
  locationLabel,
}: {
  weather: WeatherResponse;
  locationLabel: string | null;
}) {
  const imperial = weather.units === "imperial";
  const tempUnit = imperial ? "°F" : "°C";

  const rows: { label: string; value: string }[] = [
    { label: "Feels like", value: `${Math.round(weather.feelsLike)}${tempUnit}` },
    { label: "Humidity", value: `${weather.humidity}%` },
    {
      label: "Wind",
      value: `${weather.windSpeed.toFixed(1)} ${imperial ? "mph" : "km/h"} ${degToCompass(weather.windDirection)}`,
    },
    {
      label: "Pressure",
      value: imperial
        ? `${weather.pressure.toFixed(2)} inHg`
        : `${Math.round(weather.pressure)} hPa`,
    },
    { label: "Cloud cover", value: `${weather.cloudCover}%` },
  ];

  // Nullable fields: render only when present (stable schema => key always exists).
  if (weather.uvIndex != null) {
    rows.push({ label: "UV index", value: `${weather.uvIndex}` });
  }
  if (weather.precipitation != null) {
    rows.push({
      label: "Precipitation",
      value: imperial
        ? `${weather.precipitation.toFixed(2)} in`
        : `${weather.precipitation.toFixed(1)} mm`,
    });
  }
  if (weather.visibility != null) {
    rows.push({
      label: "Visibility",
      value: `${weather.visibility} ${imperial ? "ft" : "m"}`,
    });
  }

  const heading =
    locationLabel ??
    `${weather.latitude.toFixed(3)}, ${weather.longitude.toFixed(3)}`;
  const sources = weather.providers
    .map((p) => PROVIDER_LABELS[p] ?? p)
    .join(", ");

  return (
    <section className="card">
      <h2>{heading}</h2>
      {weather.condition && <p className="condition">{weather.condition}</p>}

      <div className="temp">
        {Math.round(weather.temperature)}
        <span className="temp-unit">{tempUnit}</span>
      </div>

      <dl className="details">
        {rows.map((r) => (
          <div className="detail" key={r.label}>
            <dt>{r.label}</dt>
            <dd>{r.value}</dd>
          </div>
        ))}
      </dl>

      <p className="sources">
        {sources ? `Sources: ${sources}` : "No sources reported"}
      </p>
    </section>
  );
}
