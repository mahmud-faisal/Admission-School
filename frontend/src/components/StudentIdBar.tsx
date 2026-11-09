import { useState, type ChangeEvent } from 'react';
import { isValidStudentId, useStudentId } from '../state/StudentIdContext';

export const STUDENT_ID_HINT = 'Birth registration number must be exactly 17 digits.';

/** The student's identity: birth registration number, saved in localStorage once it is valid. */
export function StudentIdBar() {
  const { studentId, updateStudentId } = useStudentId();
  const [draft, setDraft] = useState(studentId);

  const onChange = (event: ChangeEvent<HTMLInputElement>) => {
    const digitsOnly = event.target.value.replace(/\D/g, '').slice(0, 17);
    setDraft(digitsOnly);
    updateStudentId(digitsOnly);
  };

  const valid = isValidStudentId(draft);

  return (
    <div className="student-bar">
      <label htmlFor="student-id">Birth registration number</label>
      <input
        id="student-id"
        name="studentId"
        inputMode="numeric"
        autoComplete="off"
        placeholder="17 digits, e.g. 20152691234567890"
        value={draft}
        onChange={onChange}
        aria-invalid={draft !== '' && !valid}
        aria-describedby="student-id-status"
      />
      <span id="student-id-status" className={valid ? 'hint ok' : 'hint'} role="status">
        {valid
          ? 'Saved. You are applying as this student.'
          : draft === ''
            ? 'Enter the student’s 17-digit birth registration number to apply.'
            : `${STUDENT_ID_HINT} (${draft.length}/17)`}
      </span>
    </div>
  );
}
