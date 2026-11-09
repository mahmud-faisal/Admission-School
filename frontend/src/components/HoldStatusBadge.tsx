import type { HoldStatus } from '../api/types';

const LABELS: Record<HoldStatus, string> = {
  HELD: 'Seat held, waiting for payment',
  CONFIRMED: 'Admission confirmed',
  CANCELLED: 'Cancelled',
  EXPIRED: 'Expired, seat released',
};

export function HoldStatusBadge({ status }: { status: HoldStatus }) {
  return (
    <span className={`badge status-${status.toLowerCase()}`} title={LABELS[status]} data-testid="hold-status">
      {status}
    </span>
  );
}

export function statusDescription(status: HoldStatus): string {
  return LABELS[status];
}
