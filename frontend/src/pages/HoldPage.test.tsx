import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { MESSAGES } from '../api/errors';
import { API, SID, hold, problem, server } from '../test/server';
import { renderApp } from '../test/utils';

describe('HoldPage', () => {
  it('shows status and a countdown', async () => {
    server.use(http.get(`${API}/holds/h_1`, () => HttpResponse.json(hold())));
    renderApp('/holds/h_1', { studentId: SID });

    expect(await screen.findByTestId('hold-status')).toHaveTextContent('HELD');
    // About 10 minutes left (mm:ss, rounded up to the next whole second).
    const [mm, ss] = screen.getByTestId('countdown').textContent!.split(':').map(Number);
    expect(mm * 60 + ss).toBeGreaterThanOrEqual(590);
    expect(mm * 60 + ss).toBeLessThanOrEqual(601);
  });

  it('switches to EXPIRED and disables Pay when time runs out, without a reload', async () => {
    // The server keeps answering HELD (as if no poll happened yet); the UI must still flip on its own clock.
    const expiresAt = new Date(Date.now() + 1500).toISOString();
    server.use(http.get(`${API}/holds/h_1`, () => HttpResponse.json(hold({ expiresAt, serverTime: new Date().toISOString() }))));
    renderApp('/holds/h_1', { studentId: SID });

    const pay = await screen.findByRole('button', { name: 'Pay admission fee' });
    expect(pay).toBeEnabled();

    await waitFor(() => expect(screen.getByTestId('hold-status')).toHaveTextContent('EXPIRED'), { timeout: 4000 });
    expect(screen.getByRole('button', { name: 'Pay admission fee' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled();
    expect(screen.getByText(MESSAGES.HOLD_EXPIRED)).toBeInTheDocument();
  });

  it('pays the admission fee and shows CONFIRMED', async () => {
    let status: 'HELD' | 'CONFIRMED' = 'HELD';
    server.use(
      http.get(`${API}/holds/h_1`, () => HttpResponse.json(hold({ status }))),
      http.post(`${API}/holds/h_1/confirm`, ({ request }) => {
        expect(request.headers.get('X-Student-Id')).toBe(SID);
        status = 'CONFIRMED';
        return HttpResponse.json({ holdId: 'h_1', status: 'CONFIRMED' });
      }),
    );
    renderApp('/holds/h_1', { studentId: SID });

    await userEvent.click(await screen.findByRole('button', { name: 'Pay admission fee' }));

    await waitFor(() => expect(screen.getByTestId('hold-status')).toHaveTextContent('CONFIRMED'));
    expect(screen.getByText(/admission is confirmed/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Pay admission fee' })).toBeDisabled();
  });

  it('shows the expired message when the server says HOLD_EXPIRED on pay', async () => {
    let status: 'HELD' | 'EXPIRED' = 'HELD';
    server.use(
      http.get(`${API}/holds/h_1`, () => HttpResponse.json(hold({ status }))),
      http.post(`${API}/holds/h_1/confirm`, () => {
        status = 'EXPIRED';
        return problem(409, 'HOLD_EXPIRED');
      }),
    );
    renderApp('/holds/h_1', { studentId: SID });

    await userEvent.click(await screen.findByRole('button', { name: 'Pay admission fee' }));

    expect(await screen.findByText(MESSAGES.HOLD_EXPIRED)).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('hold-status')).toHaveTextContent('EXPIRED'));
  });

  it('cancels the application', async () => {
    let status: 'HELD' | 'CANCELLED' = 'HELD';
    server.use(
      http.get(`${API}/holds/h_1`, () => HttpResponse.json(hold({ status }))),
      http.delete(`${API}/holds/h_1`, () => {
        status = 'CANCELLED';
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderApp('/holds/h_1', { studentId: SID });

    await userEvent.click(await screen.findByRole('button', { name: 'Cancel' }));

    await waitFor(() => expect(screen.getByTestId('hold-status')).toHaveTextContent('CANCELLED'));
  });

  it('does not let a different student pay', async () => {
    server.use(http.get(`${API}/holds/h_1`, () => HttpResponse.json(hold({ studentId: '20152691234567891' }))));
    renderApp('/holds/h_1', { studentId: SID });

    expect(await screen.findByText(MESSAGES.FORBIDDEN)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Pay admission fee' })).toBeDisabled();
  });

  it('explains when the application does not exist', async () => {
    server.use(http.get(`${API}/holds/h_404`, () => problem(404, 'NOT_FOUND')));
    renderApp('/holds/h_404', { studentId: SID });

    expect(await screen.findByRole('alert')).toHaveTextContent(MESSAGES.NOT_FOUND);
  });
});
