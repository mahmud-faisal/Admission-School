import { screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { API, SID, myHold, server } from '../test/server';
import { renderApp } from '../test/utils';

describe('MyHoldsPage', () => {
  it('lists the stored student’s applications after a reload, with each status', async () => {
    server.use(
      http.get(`${API}/me/holds`, ({ request }) => {
        expect(request.headers.get('X-Student-Id')).toBe(SID);
        return HttpResponse.json([
          myHold({ holdId: 'h_2', className: 'Class 7 - Day Shift', status: 'CONFIRMED' }),
          myHold({ holdId: 'h_1' }),
        ]);
      }),
    );

    // localStorage already holds the ID, like after refreshing or reopening the browser.
    renderApp('/me', { studentId: SID });

    const rows = await screen.findAllByRole('row');
    expect(within(rows[1]).getByRole('rowheader')).toHaveTextContent('Class 7 - Day Shift');
    expect(within(rows[1]).getByTestId('hold-status')).toHaveTextContent('CONFIRMED');
    expect(within(rows[2]).getByTestId('hold-status')).toHaveTextContent('HELD');
    expect(within(rows[2]).getByRole('link', { name: 'Pay or cancel' })).toHaveAttribute('href', '/holds/h_1');
  });

  it('shows a held application as EXPIRED once its time has passed', async () => {
    server.use(
      http.get(`${API}/me/holds`, () =>
        HttpResponse.json([myHold({ expiresAt: new Date(Date.now() - 1000).toISOString() })]),
      ),
    );
    renderApp('/me', { studentId: SID });

    expect(await screen.findByTestId('hold-status')).toHaveTextContent('EXPIRED');
  });

  it('asks for the birth registration number when none is stored', () => {
    renderApp('/me');

    expect(screen.getByText(/to see their applications/i)).toBeInTheDocument();
  });
});
