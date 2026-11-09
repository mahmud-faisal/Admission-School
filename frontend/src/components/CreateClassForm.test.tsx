import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { API, classSeats, problem, server } from '../test/server';
import { renderApp } from '../test/utils';

async function fill(name: string, seats: string) {
  if (name) await userEvent.type(screen.getByLabelText('Class name'), name);
  if (seats) await userEvent.type(screen.getByLabelText('Total seats'), seats);
  await userEvent.click(screen.getByRole('button', { name: 'Create class' }));
}

describe('CreateClassForm', () => {
  it.each([
    ['', '10', 'Please enter a class name.'],
    ['Class 6', '', 'Total seats must be a whole number.'],
    ['Class 6', '0', 'Total seats must be between 1 and 100000.'],
    ['Class 6', '2.5', 'Total seats must be a whole number.'],
  ])('validates name=%j seats=%j before calling the server', async (name, seats, message) => {
    renderApp('/admin/classes/new');

    await fill(name, seats);

    expect(screen.getByRole('alert')).toHaveTextContent(message);
  });

  it('shows the server’s field message for a 400', async () => {
    server.use(
      http.post(`${API}/classes`, () =>
        problem(400, 'VALIDATION_FAILED', { errors: { name: ['Class name must be at most 200 characters.'] } }),
      ),
    );
    renderApp('/admin/classes/new');

    await fill('Class 6', '10');

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Please check the form: Class name must be at most 200 characters.',
    );
  });

  it('creates the class and returns to the class list with a confirmation', async () => {
    server.use(
      http.post(`${API}/classes`, async ({ request }) => {
        expect(await request.json()).toEqual({ name: 'Class 9 - Day Shift', totalSeats: 40 });
        return HttpResponse.json(classSeats({ id: 'c_9', name: 'Class 9 - Day Shift', totalSeats: 40 }), { status: 201 });
      }),
    );
    renderApp('/admin/classes/new');

    await fill('Class 9 - Day Shift', '40');

    expect(await screen.findByRole('heading', { name: 'Classes' })).toBeInTheDocument();
    expect(screen.getByText(/was created with 40 seats/)).toBeInTheDocument();
  });
});
