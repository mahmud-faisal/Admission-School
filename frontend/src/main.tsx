import { QueryClientProvider } from '@tanstack/react-query';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { App } from './App';
import { createQueryClient } from './api/queryClient';
import { ErrorBoundary } from './components/ErrorBoundary';
import { StudentIdProvider } from './state/StudentIdContext';
import './index.css';

const queryClient = createQueryClient();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <StudentIdProvider>
          <BrowserRouter>
            <App />
          </BrowserRouter>
        </StudentIdProvider>
      </QueryClientProvider>
    </ErrorBoundary>
  </StrictMode>,
);
