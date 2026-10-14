"use client";
import { useActionState } from "react";
import { authorizeTesla } from "@/app/settings/tesla-actions";

export function TeslaAuthorizationForm() {
  const [message, action, pending] = useActionState(authorizeTesla, "");
  return <>
    <p className="sub">Connect using your personal Tesla developer application. You’ll sign in with Tesla and choose whether to share Powerwall data.</p>
    <p className="sub">Allowed origin: <span className="mono">https://octopus-dash.vilmos.co.uk</span><br />
      Redirect URI: <span className="mono" style={{ overflowWrap: "anywhere" }}>https://octopus-dash.vilmos.co.uk/auth/tesla/callback</span></p>
    <form action={action}>
      <div className="fields">
        <div className="field"><label htmlFor="tesla-app-client">Tesla app Client ID</label>
          <input id="tesla-app-client" className="input mono" name="clientId" required maxLength={500} autoComplete="off" /></div>
        <div className="field"><label htmlFor="tesla-app-secret">Tesla app Client secret</label>
          <input id="tesla-app-secret" className="input mono" name="clientSecret" type="password" required maxLength={2000} autoComplete="off" /></div>
      </div>
      <button className="btn ghost" disabled={pending}>{pending ? "Starting authorization…" : "Authorize with Tesla"}</button>
      {message ? <p role="status" className="form-error">{message}</p> : null}
    </form>
  </>;
}
