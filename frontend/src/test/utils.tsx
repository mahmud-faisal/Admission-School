import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { App } from '../App';
import { STUDENT_ID_STORAGE_KEY, StudentIdProvider } from '../state/StudentIdContext';

export function testQueryClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
}

/** Renders the whole app (layout, student bar, routes) at `route`, like opening that URL in the browser. */
export function renderApp(route = '/', options: { studentId?: string } = {}) {
  if (options.studentId) localStorage.setItem(STUDENT_ID_STORAGE_KEY, options.studentId);
  const queryClient = testQueryClient();
  const result = render(
    <QueryClientProvider client={queryClient}>
      <StudentIdProvider>
        <MemoryRouter initialEntries={[route]}>
          <App />
        </MemoryRouter>
      </StudentIdProvider>
    </QueryClientProvider>,
  );
  return { ...result, queryClient };
}
