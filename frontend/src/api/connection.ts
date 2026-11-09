import { useSyncExternalStore } from 'react';

/** Global "can we reach the server?" flag, set by the API client and shown by ConnectionBanner. */
let serverDown = false;
const listeners = new Set<() => void>();

export function setServerReachable(reachable: boolean): void {
  if (serverDown === !reachable) return;
  serverDown = !reachable;
  listeners.forEach((listener) => listener());
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function useServerDown(): boolean {
  return useSyncExternalStore(subscribe, () => serverDown, () => false);
}
