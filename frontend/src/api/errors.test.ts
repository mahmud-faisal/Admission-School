import { ApiError, MESSAGES, NetworkError, toUserMessage } from './errors';

const api = (status: number, code?: string, extra: object = {}) =>
  new ApiError(status, { status, code, detail: 'technical detail', ...extra });

describe('toUserMessage', () => {
  it.each([
    ['SEATS_FULL', 409],
    ['DUPLICATE_APPLICATION', 409],
    ['ALREADY_ADMITTED', 409],
    ['HOLD_EXPIRED', 409],
    ['HOLD_CANCELLED', 409],
    ['HOLD_ALREADY_CONFIRMED', 409],
    ['INVALID_STUDENT_ID', 400],
    ['FORBIDDEN', 403],
    ['NOT_FOUND', 404],
  ] as const)('maps %s to a friendly message', (code, status) => {
    expect(toUserMessage(api(status, code))).toBe(MESSAGES[code]);
  });

  it('shows the first field error for VALIDATION_FAILED', () => {
    const error = api(400, 'VALIDATION_FAILED', { errors: { totalSeats: ['Total seats must be a whole number.'] } });
    expect(toUserMessage(error)).toBe('Please check the form: Total seats must be a whole number.');
  });

  it('explains that the server is unreachable for network errors', () => {
    expect(toUserMessage(new NetworkError())).toBe(MESSAGES.NETWORK);
  });

  it('never shows technical details for 5xx', () => {
    expect(toUserMessage(api(500, 'INTERNAL_ERROR'))).toBe(MESSAGES.SERVER);
    expect(toUserMessage(new ApiError(502, undefined))).toBe(MESSAGES.SERVER);
  });

  it('handles unknown errors with a generic, non-empty message', () => {
    expect(toUserMessage(new Error('boom'))).toBe(MESSAGES.UNKNOWN);
    expect(toUserMessage(undefined)).toBe(MESSAGES.UNKNOWN);
  });
});
