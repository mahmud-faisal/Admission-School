import { useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { errorCode } from '../api/errors';
import { useApply, useClasses, useMyHolds } from '../api/hooks';
import type { ClassSeats } from '../api/types';
import { ClassTable } from '../components/ClassTable';
import { ErrorAlert, SuccessAlert } from '../components/ErrorAlert';
import { useStudentId } from '../state/StudentIdContext';

export function ClassListPage() {
  const { studentId } = useStudentId();
  const classes = useClasses();
  const myHolds = useMyHolds(studentId);
  const apply = useApply();
  const navigate = useNavigate();
  const flash = (useLocation().state as { flash?: string } | null)?.flash;
  const [applyError, setApplyError] = useState<{ className: string; error: unknown } | null>(null);

  const confirmedHold = studentId
    ? myHolds.data?.find((h) => h.status === 'CONFIRMED')
    : undefined;

  const onApply = (cls: ClassSeats) => {
    setApplyError(null);
    apply.mutate(
      { classId: cls.id, studentId },
      {
        onSuccess: (hold) => navigate(`/holds/${hold.holdId}`),
        onError: (error) => setApplyError({ className: cls.name, error }),
      },
    );
  };

  return (
    <section>
      <h1>Classes</h1>
      {flash && <SuccessAlert>{flash}</SuccessAlert>}
      {!studentId && (
        <p className="note">Enter the student&rsquo;s 17-digit birth registration number above to apply for a seat.</p>
      )}
      {confirmedHold && (
        <div className="alert success" role="status" data-testid="confirmed-banner">
          Admission confirmed for <strong>{confirmedHold.className}</strong>. You cannot apply for additional classes.{' '}
          <Link to={`/holds/${confirmedHold.holdId}`}>View admission</Link>
        </div>
      )}
      {applyError && (
        <ErrorAlert error={applyError.error}>
          {' '}
          <span className="muted">({applyError.className})</span>
          {(errorCode(applyError.error) === 'DUPLICATE_APPLICATION' ||
            errorCode(applyError.error) === 'ALREADY_ADMITTED') && (
            <>
              {' '}
              <Link to="/me">Open My applications</Link>
            </>
          )}
        </ErrorAlert>
      )}
      {classes.error && !classes.data && <ErrorAlert error={classes.error} />}
      {classes.isPending && !classes.error && <p>Loading classes&hellip;</p>}
      {classes.data && classes.data.length === 0 && (
        <p className="panel">
          No classes yet. <Link to="/admin/classes/new">Create the first class</Link>.
        </p>
      )}
      {classes.data && classes.data.length > 0 && (
        <>
          <ClassTable
            classes={classes.data}
            canApply={studentId !== ''}
            isAdmitted={Boolean(confirmedHold)}
            applyingClassId={apply.isPending ? apply.variables?.classId : undefined}
            onApply={onApply}
          />
          <p className="muted small">Seat counts update automatically every few seconds.</p>
        </>
      )}
    </section>
  );
}
