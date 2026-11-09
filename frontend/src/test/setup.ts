import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { setServerReachable } from '../api/connection';
import { POLL } from '../api/hooks';
import { resetServerClock } from '../time/serverClock';
import { server } from './server';

const defaultPoll = { ...POLL };

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));

afterEach(() => {
  cleanup();
  server.resetHandlers();
  localStorage.clear();
  resetServerClock();
  setServerReachable(true);
  Object.assign(POLL, defaultPoll);
  delete window.__ENV__;
});

afterAll(() => server.close());
