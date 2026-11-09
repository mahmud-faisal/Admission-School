import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';

export const STUDENT_ID_STORAGE_KEY = 'admission.studentId';
const STUDENT_ID_PATTERN = /^[0-9]{17}$/;

export function isValidStudentId(value: string): boolean {
  return STUDENT_ID_PATTERN.test(value);
}

function readStored(): string {
  try {
    const value = localStorage.getItem(STUDENT_ID_STORAGE_KEY);
    return value && isValidStudentId(value) ? value : '';
  } catch {
    return ''; // storage blocked (private mode etc.)
  }
}

function writeStored(value: string | null): void {
  try {
    if (value) localStorage.setItem(STUDENT_ID_STORAGE_KEY, value);
    else localStorage.removeItem(STUDENT_ID_STORAGE_KEY);
  } catch {
    // ignore: the app still works for this tab
  }
}

interface StudentIdState {
  /** The active, VALID 17-digit ID, or '' when none is entered. */
  studentId: string;
  /** Called with the raw field value on every change. */
  updateStudentId: (raw: string) => void;
}

const StudentIdContext = createContext<StudentIdState | null>(null);

export function StudentIdProvider({ children }: { children: ReactNode }) {
  const [studentId, setStudentId] = useState(readStored);

  const updateStudentId = useCallback((raw: string) => {
    if (isValidStudentId(raw)) {
      setStudentId(raw);
      writeStored(raw); // survives refresh and browser restart
    } else {
      setStudentId('');
      if (raw === '') writeStored(null); // keep the last valid ID while the user is mid-edit
    }
  }, []);

  const value = useMemo(() => ({ studentId, updateStudentId }), [studentId, updateStudentId]);
  return <StudentIdContext.Provider value={value}>{children}</StudentIdContext.Provider>;
}

export function useStudentId(): StudentIdState {
  const context = useContext(StudentIdContext);
  if (!context) throw new Error('useStudentId must be used inside StudentIdProvider');
  return context;
}
