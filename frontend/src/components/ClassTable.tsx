import type { ClassSeats } from '../api/types';

interface Props {
  classes: ClassSeats[];
  canApply: boolean;
  applyingClassId?: string;
  onApply: (cls: ClassSeats) => void;
}

export function ClassTable({ classes, canApply, applyingClassId, onApply }: Props) {
  return (
    <table className="table">
      <thead>
        <tr>
          <th scope="col">Class</th>
          <th scope="col" className="num">Total</th>
          <th scope="col" className="num">Held</th>
          <th scope="col" className="num">Confirmed</th>
          <th scope="col" className="num">Available</th>
          <th scope="col">
            <span className="sr-only">Action</span>
          </th>
        </tr>
      </thead>
      <tbody>
        {classes.map((cls) => {
          const full = cls.availableSeats <= 0;
          const busy = applyingClassId === cls.id;
          return (
            <tr key={cls.id} data-testid={`class-row-${cls.id}`}>
              <th scope="row">{cls.name}</th>
              <td className="num">{cls.totalSeats}</td>
              <td className="num">{cls.heldSeats}</td>
              <td className="num">{cls.confirmedSeats}</td>
              <td className="num">{full ? <span className="full">Full</span> : cls.availableSeats}</td>
              <td>
                <button
                  type="button"
                  onClick={() => onApply(cls)}
                  disabled={!canApply || full || busy}
                  aria-label={`Apply for ${cls.name}`}
                  title={!canApply ? 'Enter a 17-digit birth registration number first' : full ? 'No seats available' : undefined}
                >
                  {busy ? 'Applying…' : 'Apply'}
                </button>
              </td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
