import type { ProblemDetails } from './types';

/** The server answered with an error status (4xx/5xx). */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string | undefined;
  readonly problem: ProblemDetails | undefined;

  constructor(status: number, problem: ProblemDetails | undefined) {
    super(problem?.detail ?? problem?.title ?? `Request failed with status ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.code = problem?.code;
    this.problem = problem;
  }
}

/** The server could not be reached at all (down, wrong URL, CORS, timeout). */
export class NetworkError extends Error {
  constructor(message = 'Network request failed') {
    super(message);
    this.name = 'NetworkError';
  }
}

export const MESSAGES = {
  SEATS_FULL:
    'Sorry, all seats in this class are taken right now. Seats may free up if other applications expire, so please try again later.',
  DUPLICATE_APPLICATION:
    'You already have an active application or admission for this class. See My applications.',
  ALREADY_ADMITTED: 'You already have a confirmed admission. Additional applications are not permitted.',
  HOLD_EXPIRED: 'This application has expired, so the seat was released. You can apply again if seats are available.',
  HOLD_CANCELLED: 'This application was cancelled.',
  HOLD_ALREADY_CONFIRMED: "Admission is already confirmed; it can't be cancelled here.",
  INVALID_STUDENT_ID: 'Please enter a valid 17-digit birth registration number at the top of the page.',
  FORBIDDEN: 'This application belongs to a different birth registration number.',
  NOT_FOUND: "We couldn't find that class or application.",
  NETWORK: "Can't reach the admission server. Check your connection; we'll keep retrying.",
  SERVER: 'Something went wrong on the server. Please try again in a moment.',
  VALIDATION_PREFIX: 'Please check the form: ',
  UNKNOWN: 'Something went wrong. Please try again.',
} as const;

function firstFieldError(problem: ProblemDetails | undefined): string | undefined {
  const errors = problem?.errors;
  if (!errors) return undefined;
  for (const messages of Object.values(errors)) {
    if (messages?.length) return messages[0];
  }
  return undefined;
}

/** Turns any error into a sentence a parent can understand. Never returns an empty string. */
export function toUserMessage(error: unknown): string {
  if (error instanceof NetworkError) return MESSAGES.NETWORK;
  if (error instanceof ApiError) {
    switch (error.code) {
      case 'SEATS_FULL':
      case 'DUPLICATE_APPLICATION':
      case 'ALREADY_ADMITTED':
      case 'HOLD_EXPIRED':
      case 'HOLD_CANCELLED':
      case 'HOLD_ALREADY_CONFIRMED':
      case 'INVALID_STUDENT_ID':
      case 'FORBIDDEN':
      case 'NOT_FOUND':
        return MESSAGES[error.code];
      case 'VALIDATION_FAILED':
        return MESSAGES.VALIDATION_PREFIX + (firstFieldError(error.problem) ?? error.problem?.detail ?? 'some values are invalid.');
      default:
        if (error.status >= 500) return MESSAGES.SERVER;
        if (error.status === 404) return MESSAGES.NOT_FOUND;
        return error.problem?.detail ?? MESSAGES.UNKNOWN;
    }
  }
  return MESSAGES.UNKNOWN;
}

export function errorCode(error: unknown): string | undefined {
  return error instanceof ApiError ? error.code : undefined;
}
