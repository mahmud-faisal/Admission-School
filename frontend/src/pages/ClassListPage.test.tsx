import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { POLL } from '../api/hooks';
import { MESSAGES } from '../api/errors';
import { API, SID, classSeats, hold, problem, server } from '../test/server';
import { renderApp } from '../test/utils';

describe('ClassListPage', () => {
  it('shows total, held, confirmed and available seats for each class', async () => {
    renderApp('/');

    const row = await screen.findByTestId('class-row-c_1');
    const cells = within(row).getAllByRole('cell').map((c) => c.textContent);
    expect(within(row).getByRole('rowheader')).toHaveTextContent('Class 6 - Morning Shift');
    expect(cells.slice(0, 4)).toEqual(['120', '15', '80', '25']);
  });

  it('updates seat counts by polling, without a page reload', async () => {
    POLL.classesMs = 100;
    let available = 25;
    server.use(http.get(`${API}/classes`, () => HttpResponse.json([classSeats({ availableSeats: available })])));
    renderApp('/');
    const row = await screen.findByTestId('class-row-c_1');
    expect(within(row).getAllByRole('cell')[3]).toHaveTextContent('25');

    available = 7;

    await waitFor(() => expect(within(row).getAllByRole('cell')[3]).toHaveTextContent('7'), { timeout: 2000 });
  });

  it('disables Apply until a valid student ID is entered', async () => {
    renderApp('/');

    expect(await screen.findByRole('button', { name: /apply for class 6/i })).toBeDisabled();
    expect(screen.getByText(/enter the student.s 17-digit birth registration number above/i)).toBeInTheDocument();
  });

  it('applies and opens the application page', async () => {
    server.use(
      http.post(`${API}/classes/c_1/holds`, ({ request }) => {
        expect(request.headers.get('X-Student-Id')).toBe(SID);
        return HttpResponse.json(hold(), { status: 201 });
      }),
      http.get(`${API}/holds/h_1`, () => HttpResponse.json(hold())),
    );
    renderApp('/', { studentId: SID });

    await userEvent.click(await screen.findByRole('button', { name: /apply for class 6/i }));

    expect(await screen.findByRole('heading', { name: 'Application' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Pay admission fee' })).toBeEnabled();
  });

  it('shows a clear message when the class is full', async () => {
    server.use(http.post(`${API}/classes/c_1/holds`, () => problem(409, 'SEATS_FULL')));
    renderApp('/', { studentId: SID });

    await userEvent.click(await screen.findByRole('button', { name: /apply for class 6/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(MESSAGES.SEATS_FULL);
  });

  it('marks a full class and disables its Apply button', async () => {
    server.use(http.get(`${API}/classes`, () => HttpResponse.json([classSeats({ availableSeats: 0, heldSeats: 40 })])));
    renderApp('/', { studentId: SID });

    expect(await screen.findByText('Full')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /apply for class 6/i })).toBeDisabled();
  });
});
