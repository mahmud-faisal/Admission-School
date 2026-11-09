import { useEffect, useState } from 'react';

/**
 * Keeps an estimate of (server clock − browser clock) so countdowns are right even if the user's clock is off.
 * Samples come from `serverTime` in hold responses (database clock) or the X-Server-Time header.
 */
let offsetMs = 0;
let hasSample = false;

export function recordServerTime(serverIso: string | null | undefined, requestStartMs: number, responseEndMs: number): void {
  if (!serverIso) return;
  const server = Date.parse(serverIso);
  if (Number.isNaN(server)) return;
  const sample = server - (requestStartMs + responseEndMs) / 2;
  offsetMs = hasSample ? offsetMs * 0.5 + sample * 0.5 : sample;
  hasSample = true;
}

export function serverNow(): number {
  return Date.now() + offsetMs;
}

export function resetServerClock(): void {
  offsetMs = 0;
  hasSample = false;
}

/** Current server time, re-rendering every `tickMs`. */
export function useServerNow(tickMs = 1000): number {
  const [now, setNow] = useState(serverNow);
  useEffect(() => {
    const timer = setInterval(() => setNow(serverNow()), tickMs);
    return () => clearInterval(timer);
  }, [tickMs]);
  return now;
}

/** "mm:ss" (or "h:mm:ss") for a number of milliseconds, never negative. */
export function formatRemaining(ms: number): string {
  const total = Math.max(0, Math.ceil(ms / 1000));
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  const mmss = `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
  return h > 0 ? `${h}:${mmss}` : mmss;
}
