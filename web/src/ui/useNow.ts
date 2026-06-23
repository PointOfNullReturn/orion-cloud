import { useEffect, useState } from "react";

// Returns the current time, re-rendering the caller every `intervalMs` so that
// time-relative text (e.g. "14 min ago") advances on its own between data
// fetches. Thin glue — the actual formatting it drives lives in datetime.ts.
export function useNow(intervalMs: number): Date {
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs]);

  return now;
}
