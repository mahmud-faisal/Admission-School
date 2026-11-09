import type { ReactNode } from 'react';
import { toUserMessage } from '../api/errors';

interface Props {
  error?: unknown;
  message?: string;
  children?: ReactNode;
}

/** A visible, screen-reader-announced error message in plain language. */
export function ErrorAlert({ error, message, children }: Props) {
  if (!error && !message) return null;
  return (
    <div className="alert error" role="alert">
      <span>{message ?? toUserMessage(error)}</span>
      {children}
    </div>
  );
}

export function SuccessAlert({ children }: { children: ReactNode }) {
  return (
    <div className="alert success" role="status">
      {children}
    </div>
  );
}
