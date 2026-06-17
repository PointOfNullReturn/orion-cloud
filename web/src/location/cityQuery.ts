import type { GeocodeResult } from "../api/geocode";

export interface ParsedQuery {
  name: string;
  hint: string | null;
}

// "Sheffield, AL" -> { name: "Sheffield", hint: "AL" }. Open-Meteo's geocoder
// only understands the bare place name, so we search by `name` and use `hint`
// to re-rank the results client-side.
export function parseCityQuery(input: string): ParsedQuery {
  const trimmed = input.trim();
  const comma = trimmed.indexOf(",");
  if (comma === -1) {
    return { name: trimmed, hint: null };
  }
  const name = trimmed.slice(0, comma).trim();
  const hint = trimmed.slice(comma + 1).trim();
  return { name, hint: hint.length > 0 ? hint : null };
}

// Stable-partition results so those matching the hint come first.
export function prioritizeByHint(
  results: GeocodeResult[],
  hint: string | null,
): GeocodeResult[] {
  if (!hint) return results;
  const matches = results.filter((r) => matchesHint(r, hint));
  const rest = results.filter((r) => !matchesHint(r, hint));
  return [...matches, ...rest];
}

function matchesHint(r: GeocodeResult, hint: string): boolean {
  const h = hint.trim().toLowerCase();
  if (!h) return false;

  const region = r.region?.toLowerCase() ?? "";
  const country = r.country?.toLowerCase() ?? "";
  const code = r.countryCode?.toLowerCase() ?? "";

  if (region.includes(h) || country.includes(h) || code === h) {
    return true;
  }

  // US state abbreviation (AL -> Alabama), since Open-Meteo returns full names.
  const expanded = US_STATES[hint.trim().toUpperCase()];
  return expanded != null && region === expanded.toLowerCase();
}

const US_STATES: Record<string, string> = {
  AL: "Alabama", AK: "Alaska", AZ: "Arizona", AR: "Arkansas", CA: "California",
  CO: "Colorado", CT: "Connecticut", DE: "Delaware", FL: "Florida", GA: "Georgia",
  HI: "Hawaii", ID: "Idaho", IL: "Illinois", IN: "Indiana", IA: "Iowa",
  KS: "Kansas", KY: "Kentucky", LA: "Louisiana", ME: "Maine", MD: "Maryland",
  MA: "Massachusetts", MI: "Michigan", MN: "Minnesota", MS: "Mississippi", MO: "Missouri",
  MT: "Montana", NE: "Nebraska", NV: "Nevada", NH: "New Hampshire", NJ: "New Jersey",
  NM: "New Mexico", NY: "New York", NC: "North Carolina", ND: "North Dakota", OH: "Ohio",
  OK: "Oklahoma", OR: "Oregon", PA: "Pennsylvania", RI: "Rhode Island", SC: "South Carolina",
  SD: "South Dakota", TN: "Tennessee", TX: "Texas", UT: "Utah", VT: "Vermont",
  VA: "Virginia", WA: "Washington", WV: "West Virginia", WI: "Wisconsin", WY: "Wyoming",
  DC: "District of Columbia",
};
