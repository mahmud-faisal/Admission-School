import { formatRemaining } from '../time/serverClock';

/** Time left until `expiresAt`, measured on the (server-corrected) clock passed in as `now`. */
export function Countdown({ expiresAt, now }: { expiresAt: string; now: number }) {
  const remaining = Date.parse(expiresAt) - now;
  return (
    <span className={remaining <= 60_000 ? 'countdown urgent' : 'countdown'} aria-label="Time remaining" data-testid="countdown">
      {formatRemaining(remaining)}
    </span>
  );
}
