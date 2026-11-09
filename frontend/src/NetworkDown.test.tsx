import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { MESSAGES } from './api/errors';
import { API, server } from './test/server';
import { renderApp } from './test/utils';

describe('when the server is down', () => {
  it('shows an understandable message instead of a blank page', async () => {
    server.use(http.get(`${API}/classes`, () => HttpResponse.error()));

    renderApp('/');

    const alerts = await screen.findAllByRole('alert');
    expect(alerts.some((a) => a.textContent?.includes(MESSAGES.NETWORK))).toBe(true);
    expect(screen.getByRole('heading', { name: 'Classes' })).toBeInTheDocument();
  });

  it('shows a friendly message for a 500 error', async () => {
    server.use(http.get(`${API}/classes`, () => HttpResponse.json({ code: 'INTERNAL_ERROR' }, { status: 500 })));

    renderApp('/');

    expect(await screen.findByRole('alert')).toHaveTextContent(MESSAGES.SERVER);
  });
});
