import { QueryClient } from '@tanstack/react-query';
import { ApiError } from './errors';

/** Retries only when it might help (network/5xx), never for 4xx business errors. */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: (failureCount, error) => !(error instanceof ApiError && error.status < 500) && failureCount < 1,
        refetchOnWindowFocus: true,
      },
      mutations: { retry: 0 },
    },
  });
}
