import { apiBaseUrl } from '../config';
import { recordServerTime } from '../time/serverClock';
import { setServerReachable } from './connection';
import { ApiError, NetworkError } from './errors';
import type { ProblemDetails } from './types';

export const REQUEST_TIMEOUT_MS = 10_000;
export const STUDENT_ID_HEADER = 'X-Student-Id';

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'DELETE';
  body?: unknown;
  studentId?: string;
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

// A timer-based timeout instead of AbortSignal: jsdom's AbortSignal is rejected by Node's fetch in tests.
function withTimeout<T>(promise: Promise<T>, ms: number): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const timer = setTimeout(() => reject(new NetworkError('Request timed out')), ms);
    promise.then(
      (value) => {
        clearTimeout(timer);
        resolve(value);
      },
      (error: unknown) => {
        clearTimeout(timer);
        reject(error);
      },
    );
  });
}

/**
 * Calls the API. Throws NetworkError if the server cannot be reached and ApiError (with the ProblemDetails
 * `code`) for 4xx/5xx. Returns undefined for 204.
 */
export async function fetchJson<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (options.body !== undefined) headers['Content-Type'] = 'application/json';
  if (options.studentId) headers[STUDENT_ID_HEADER] = options.studentId;

  const started = Date.now();
  let response: Response;
  try {
    response = await withTimeout(
      fetch(`${apiBaseUrl()}${path}`, {
        method: options.method ?? 'GET',
        headers,
        body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
      }),
      REQUEST_TIMEOUT_MS,
    );
  } catch (error) {
    setServerReachable(false);
    throw error instanceof NetworkError ? error : new NetworkError();
  }
  const ended = Date.now();
  setServerReachable(true);

  let text: string;
  try {
    text = await response.text();
  } catch {
    // The connection dropped while the body was being read.
    setServerReachable(false);
    throw new NetworkError();
  }
  let data: unknown;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = undefined;
    }
  }

  if (!response.ok) {
    throw new ApiError(response.status, isObject(data) ? (data as ProblemDetails) : undefined);
  }

  // Prefer the database clock carried in hold bodies; fall back to the header.
  const bodyTime = isObject(data) && typeof data.serverTime === 'string' ? data.serverTime : null;
  recordServerTime(bodyTime ?? response.headers.get('X-Server-Time'), started, ended);

  return data as T;
}
