import { apiBaseUrl, DEFAULT_API_BASE_URL } from './config';

describe('apiBaseUrl', () => {
  it('uses the runtime window.__ENV__.API_BASE_URL over the build-time value', () => {
    vi.stubEnv('VITE_API_BASE_URL', 'http://build-time:8080');
    window.__ENV__ = { API_BASE_URL: 'http://runtime:9090/' };

    expect(apiBaseUrl()).toBe('http://runtime:9090');
  });

  it('falls back to VITE_API_BASE_URL when no runtime value is set', () => {
    vi.stubEnv('VITE_API_BASE_URL', 'http://build-time:8080');
    window.__ENV__ = { API_BASE_URL: '' };

    expect(apiBaseUrl()).toBe('http://build-time:8080');
  });

  it('defaults to localhost:8080', () => {
    vi.stubEnv('VITE_API_BASE_URL', '');

    expect(apiBaseUrl()).toBe(DEFAULT_API_BASE_URL);
  });
});
