"use client";
export default function ErrorPage({ reset }: { reset: () => void }) {
  return (
    <main className="panel page-error">
      <h1>Energy data is unavailable</h1>
      <p className="muted">
        The dashboard couldn’t reach the energy API. Check the service health
        and try again.
      </p>
      <button className="btn" onClick={reset}>
        Try again
      </button>
    </main>
  );
}
