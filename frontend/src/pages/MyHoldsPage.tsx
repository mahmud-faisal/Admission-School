import { Link } from 'react-router-dom';
import { useMyHolds } from '../api/hooks';
import { Countdown } from '../components/Countdown';
import { displayStatus } from '../components/displayStatus';
import { ErrorAlert } from '../components/ErrorAlert';
import { HoldStatusBadge } from '../components/HoldStatusBadge';
import { useStudentId } from '../state/StudentIdContext';
import { useServerNow } from '../time/serverClock';

export function MyHoldsPage() {
  const { studentId } = useStudentId();
  const holds = useMyHolds(studentId);
  const now = useServerNow(1000);

  return (
    <section>
      <h1>My applications</h1>
      {!studentId && (
        <p className="note">Enter the student&rsquo;s 17-digit birth registration number above to see their applications.</p>
      )}
      {studentId && holds.error && !holds.data && <ErrorAlert error={holds.error} />}
      {studentId && holds.isPending && !holds.error && <p>Loading applications&hellip;</p>}
      {studentId && holds.data && holds.data.length === 0 && (
        <p className="panel">
          No applications yet. <Link to="/">Choose a class</Link>.
        </p>
      )}
      {studentId && holds.data && holds.data.length > 0 && (
        <table className="table">
          <thead>
            <tr>
              <th scope="col">Class</th>
              <th scope="col">Status</th>
              <th scope="col">Time remaining</th>
              <th scope="col">
                <span className="sr-only">Open</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {holds.data.map((hold) => {
              const status = displayStatus(hold.status, hold.expiresAt, now);
              return (
                <tr key={hold.holdId}>
                  <th scope="row">{hold.className}</th>
                  <td>
                    <HoldStatusBadge status={status} />
                  </td>
                  <td>{status === 'HELD' ? <Countdown expiresAt={hold.expiresAt} now={now} /> : '—'}</td>
                  <td>
                    <Link to={`/holds/${hold.holdId}`}>{status === 'HELD' ? 'Pay or cancel' : 'Details'}</Link>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </section>
  );
}
