import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { MESSAGES, errorCode } from '../api/errors';
import { queryKeys, useCancel, useConfirm, useHold } from '../api/hooks';
import { Countdown } from '../components/Countdown';
import { displayStatus } from '../components/displayStatus';
import { ErrorAlert, SuccessAlert } from '../components/ErrorAlert';
import { HoldStatusBadge, statusDescription } from '../components/HoldStatusBadge';
import { useStudentId } from '../state/StudentIdContext';
import { useServerNow } from '../time/serverClock';

export function HoldPage() {
  const { holdId = '' } = useParams();
  const { studentId } = useStudentId();
  const queryClient = useQueryClient();
  const hold = useHold(holdId);
  const confirm = useConfirm(holdId);
  const cancel = useCancel(holdId);
  const now = useServerNow(250);
  const [done, setDone] = useState<string | null>(null);

  const data = hold.data;
  const status = data ? displayStatus(data.status, data.expiresAt, now) : undefined;

  // The moment the countdown hits zero, ask the server too (it also reports EXPIRED, computed on read).
  const expiredLocally = data?.status === 'HELD' && status === 'EXPIRED';
  const refreshed = useRef(false);
  useEffect(() => {
    if (expiredLocally && !refreshed.current) {
      refreshed.current = true;
      void queryClient.invalidateQueries({ queryKey: queryKeys.hold(holdId) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.classes });
    }
  }, [expiredLocally, holdId, queryClient]);

  if (hold.isPending && !hold.error) return <p>Loading application&hellip;</p>;
  if (!data) {
    return (
      <section>
        <h1>Application</h1>
        <ErrorAlert error={hold.error} />
        <Link to="/">Back to classes</Link>
      </section>
    );
  }

  const isOwner = !data.studentId || data.studentId === studentId;
  const canAct = status === 'HELD' && studentId !== '' && isOwner;
  const busy = confirm.isPending || cancel.isPending;
  const actionError = confirm.error ?? cancel.error;

  const onPay = () => {
    setDone(null);
    cancel.reset();
    confirm.mutate(studentId, { onSuccess: () => setDone('Payment received. The admission is confirmed.') });
  };
  const onCancel = () => {
    setDone(null);
    confirm.reset();
    cancel.mutate(studentId, { onSuccess: () => setDone('The application was cancelled and the seat released.') });
  };

  return (
    <section>
      <h1>Application</h1>
      <div className="panel hold">
        <dl>
          <dt>Class</dt>
          <dd>{data.className ?? data.classId}</dd>
          <dt>Status</dt>
          <dd>
            <HoldStatusBadge status={status!} /> <span className="muted">{statusDescription(status!)}</span>
          </dd>
          {status === 'HELD' && (
            <>
              <dt>Time remaining</dt>
              <dd>
                <Countdown expiresAt={data.expiresAt} now={now} />{' '}
                <span className="muted small">(pay before {new Date(data.expiresAt).toLocaleTimeString()})</span>
              </dd>
            </>
          )}
          <dt>Application ID</dt>
          <dd className="mono">{data.holdId}</dd>
        </dl>

        {status === 'EXPIRED' && <ErrorAlert message={MESSAGES.HOLD_EXPIRED} />}
        {status === 'HELD' && studentId === '' && (
          <p className="note">Enter the birth registration number used for this application to pay or cancel.</p>
        )}
        {status === 'HELD' && studentId !== '' && !isOwner && <ErrorAlert message={MESSAGES.FORBIDDEN} />}
        {done && <SuccessAlert>{done}</SuccessAlert>}
        {actionError && errorCode(actionError) !== 'HOLD_EXPIRED' && <ErrorAlert error={actionError} />}

        <div className="actions">
          <button type="button" onClick={onPay} disabled={!canAct || busy}>
            {confirm.isPending ? 'Paying…' : 'Pay admission fee'}
          </button>
          <button type="button" className="secondary" onClick={onCancel} disabled={!canAct || busy}>
            {cancel.isPending ? 'Cancelling…' : 'Cancel'}
          </button>
        </div>
      </div>
      <p>
        <Link to="/me">My applications</Link> &middot; <Link to="/">All classes</Link>
      </p>
    </section>
  );
}
