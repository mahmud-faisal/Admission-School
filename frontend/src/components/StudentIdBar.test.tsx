import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { STUDENT_ID_STORAGE_KEY } from '../state/StudentIdContext';
import { SID } from '../test/server';
import { renderApp } from '../test/utils';

describe('StudentIdBar', () => {
  it('rejects a number that is not 17 digits and explains why', async () => {
    renderApp('/');
    const input = screen.getByLabelText('Birth registration number');

    await userEvent.type(input, '12345');

    expect(screen.getByText(/must be exactly 17 digits/i)).toBeInTheDocument();
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(localStorage.getItem(STUDENT_ID_STORAGE_KEY)).toBeNull();
  });

  it('ignores letters and other non-digits', async () => {
    renderApp('/');
    const input = screen.getByLabelText('Birth registration number');

    await userEvent.type(input, '12ab-34');

    expect(input).toHaveValue('1234');
  });

  it('saves a valid 17-digit ID to localStorage', async () => {
    renderApp('/');

    await userEvent.type(screen.getByLabelText('Birth registration number'), SID);

    expect(localStorage.getItem(STUDENT_ID_STORAGE_KEY)).toBe(SID);
    expect(screen.getByText(/saved/i)).toBeInTheDocument();
  });

  it('restores the saved ID after a reload', () => {
    renderApp('/', { studentId: SID });

    expect(screen.getByLabelText('Birth registration number')).toHaveValue(SID);
  });
});
