"use client";
import { useActionState } from "react";
import { login } from "@/app/login/actions";
export function LoginForm() {
  const [error, action, pending] = useActionState(login, "");
  return (
    <form action={action} className="panel auth-panel">
      <div className="brand">Home energy</div>
      <h1>Welcome home</h1>
      <p className="muted">Sign in to your energy dashboard.</p>
      <div className="field">
        <label htmlFor="password">Password</label>
        <input
          id="password"
          name="password"
          className="input"
          type="password"
          autoComplete="current-password"
          required
          autoFocus
        />
      </div>
      {error ? (
        <div className="form-error" role="alert">
          {error}
        </div>
      ) : null}
      <button className="btn primary" disabled={pending}>
        {pending ? "Signing in…" : "Sign in"}
      </button>
    </form>
  );
}
