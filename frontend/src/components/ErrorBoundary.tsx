import { Component, type ErrorInfo, type ReactNode } from 'react';

interface Props {
  children: ReactNode;
  /** When this changes (e.g. the route), a shown error is cleared. */
  resetKey?: string;
}

interface State {
  error: Error | null;
}

/** Catches render errors so the user never sees a blank page. */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    console.error('UI error', error, info.componentStack);
  }

  componentDidUpdate(previous: Props): void {
    if (previous.resetKey !== this.props.resetKey && this.state.error) {
      this.setState({ error: null });
    }
  }

  render() {
    if (!this.state.error) return this.props.children;
    return (
      <div className="panel error-fallback" role="alert">
        <h2>Something went wrong while showing this page.</h2>
        <p>Your applications are safe. Try reloading the page.</p>
        <div className="actions">
          <button type="button" onClick={() => window.location.reload()}>
            Reload
          </button>
          <a className="button secondary" href="/">
            Go to classes
          </a>
        </div>
      </div>
    );
  }
}
