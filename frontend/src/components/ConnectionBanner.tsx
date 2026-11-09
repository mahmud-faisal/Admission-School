import { useServerDown } from '../api/connection';
import { MESSAGES } from '../api/errors';

/** Shown on every page while the backend cannot be reached; last loaded data stays visible underneath. */
export function ConnectionBanner() {
  const down = useServerDown();
  if (!down) return null;
  return (
    <div className="alert warning banner" role="alert">
      {MESSAGES.NETWORK}
    </div>
  );
}
