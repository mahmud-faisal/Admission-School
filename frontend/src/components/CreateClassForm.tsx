import { useState, type FormEvent } from 'react';
import { useCreateClass } from '../api/hooks';
import type { ClassSeats } from '../api/types';
import { ErrorAlert } from './ErrorAlert';

export const MAX_NAME = 200;
export const MAX_SEATS = 100_000;

function validate(name: string, seats: string): string | null {
  if (name.trim() === '') return 'Please enter a class name.';
  if (name.trim().length > MAX_NAME) return `Class name must be at most ${MAX_NAME} characters.`;
  if (!/^\d+$/.test(seats.trim())) return 'Total seats must be a whole number.';
  const n = Number(seats.trim());
  if (n < 1 || n > MAX_SEATS) return `Total seats must be between 1 and ${MAX_SEATS}.`;
  return null;
}

export function CreateClassForm({ onCreated }: { onCreated: (cls: ClassSeats) => void }) {
  const [name, setName] = useState('');
  const [seats, setSeats] = useState('');
  const [clientError, setClientError] = useState<string | null>(null);
  const create = useCreateClass();

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    const problem = validate(name, seats);
    setClientError(problem);
    if (problem) return;
    create.mutate({ name: name.trim(), totalSeats: Number(seats.trim()) }, { onSuccess: onCreated });
  };

  return (
    <form className="panel form" onSubmit={onSubmit} noValidate>
      <div className="field">
        <label htmlFor="class-name">Class name</label>
        <input
          id="class-name"
          value={name}
          maxLength={MAX_NAME + 50}
          placeholder="Class 6 - Morning Shift"
          onChange={(e) => setName(e.target.value)}
        />
        <small>Each shift is its own class, e.g. &ldquo;Class 6 - Morning Shift&rdquo;.</small>
      </div>
      <div className="field">
        <label htmlFor="class-seats">Total seats</label>
        <input
          id="class-seats"
          inputMode="numeric"
          value={seats}
          placeholder="120"
          onChange={(e) => setSeats(e.target.value)}
        />
      </div>
      <ErrorAlert message={clientError ?? undefined} />
      {!clientError && <ErrorAlert error={create.error} />}
      <button type="submit" disabled={create.isPending}>
        {create.isPending ? 'Creating…' : 'Create class'}
      </button>
    </form>
  );
}
