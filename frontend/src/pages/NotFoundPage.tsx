import { Link } from 'react-router-dom';

export function NotFoundPage() {
  return (
    <section>
      <h1>Page not found</h1>
      <p>
        This page does not exist. <Link to="/">Go to classes</Link>.
      </p>
    </section>
  );
}
