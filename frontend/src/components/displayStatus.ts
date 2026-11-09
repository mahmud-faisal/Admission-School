import type { HoldStatus } from '../api/types';

/**
 * A HELD hold whose expiry has passed is shown as EXPIRED immediately, without waiting for the next poll.
 * The server computes the same thing on read with the database clock.
 */
export function displayStatus(status: HoldStatus, expiresAt: string, now: number): HoldStatus {
  return status === 'HELD' && now >= Date.parse(expiresAt) ? 'EXPIRED' : status;
}
