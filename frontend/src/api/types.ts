/** Wire types for the API contract (spec §1.4). Extra fields sent by the server are optional here. */

export type HoldStatus = 'HELD' | 'CONFIRMED' | 'CANCELLED' | 'EXPIRED';

export interface ClassSeats {
  id: string;
  name: string;
  totalSeats: number;
  heldSeats: number;
  confirmedSeats: number;
  availableSeats: number;
  createdAt?: string;
}

export interface CreateClassInput {
  name: string;
  totalSeats: number;
}

export interface HoldCreated {
  holdId: string;
  classId: string;
  studentId: string;
  status: HoldStatus;
  expiresAt: string;
  className?: string;
  serverTime?: string;
}

export interface Hold {
  holdId: string;
  classId: string;
  status: HoldStatus;
  expiresAt: string;
  studentId?: string;
  className?: string;
  createdAt?: string;
  confirmedAt?: string | null;
  cancelledAt?: string | null;
  serverTime?: string;
}

export interface HoldConfirmed {
  holdId: string;
  status: HoldStatus;
}

export interface MyHold {
  holdId: string;
  classId: string;
  className: string;
  status: HoldStatus;
  expiresAt: string;
  createdAt?: string;
  confirmedAt?: string | null;
}

/** RFC 7807 problem details with the API's machine-readable `code`. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
}
