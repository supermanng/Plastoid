// Sponsor module entry (sponsor/index.html). Providers: Query, Router at the module base, Toast, Auth.
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '../../shared/styles/index.css';
import App from './App';
import { AuthProvider } from '../../shared/lib/auth';
import { MODULES } from '../../shared/lib/navigation';
import { ToastProvider } from '../../shared/components/ui';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (count, error) => count < 1 && !(error?.status >= 400 && error?.status < 500),
      staleTime: 30_000,
      refetchOnWindowFocus: false,
    },
  },
});

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter basename={MODULES.sponsor}>
        <ToastProvider>
          <AuthProvider>
            <App />
          </AuthProvider>
        </ToastProvider>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
