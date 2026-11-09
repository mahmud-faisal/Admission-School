import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { ConnectionBanner } from './ConnectionBanner';
import { ErrorBoundary } from './ErrorBoundary';
import { StudentIdBar } from './StudentIdBar';

export function Layout() {
  const location = useLocation();
  return (
    <div className="app">
      <header className="header">
        <div className="brand">School Admission</div>
        <nav aria-label="Main">
          <NavLink to="/" end>
            Classes
          </NavLink>
          <NavLink to="/me">My applications</NavLink>
          <NavLink to="/admin/classes/new">Create class</NavLink>
        </nav>
      </header>
      <StudentIdBar />
      <ConnectionBanner />
      <main className="main">
        <ErrorBoundary resetKey={location.pathname}>
          <Outlet />
        </ErrorBoundary>
      </main>
    </div>
  );
}
