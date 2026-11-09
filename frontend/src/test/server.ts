import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';
import type { ClassSeats, Hold, MyHold, ProblemDetails } from '../api/types';

export const API = 'http://localhost:8080';

export const SID = '20152691234567890';

export function classSeats(overrides: Partial<ClassSeats> = {}): ClassSeats {
  return {
    id: 'c_1',
    name: 'Class 6 - Morning Shift',
    totalSeats: 120,
    heldSeats: 15,
    confirmedSeats: 80,
    availableSeats: 25,
    ...overrides,
  };
}

export function hold(overrides: Partial<Hold> = {}): Hold {
  return {
    holdId: 'h_1',
    classId: 'c_1',
    status: 'HELD',
    expiresAt: new Date(Date.now() + 10 * 60_000).toISOString(),
    studentId: SID,
    className: 'Class 6 - Morning Shift',
    serverTime: new Date().toISOString(),
    ...overrides,
  };
}

export function myHold(overrides: Partial<MyHold> = {}): MyHold {
  return {
    holdId: 'h_1',
    classId: 'c_1',
    className: 'Class 6 - Morning Shift',
    status: 'HELD',
    expiresAt: new Date(Date.now() + 10 * 60_000).toISOString(),
    ...overrides,
  };
}

export function problem(status: number, code: string, extra: Partial<ProblemDetails> = {}) {
  return HttpResponse.json(
    { type: 'about:blank', title: code, status, detail: code, code, ...extra },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

/** Default handlers; tests override with server.use(...). */
export const server = setupServer(
  http.get(`${API}/classes`, () => HttpResponse.json([classSeats()])),
  http.get(`${API}/me/holds`, () => HttpResponse.json([])),
);
