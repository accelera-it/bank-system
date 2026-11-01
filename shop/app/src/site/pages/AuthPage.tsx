import { type FormEvent, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";

import { api, errorMessage, localPath, openCabinet } from "../../api";

const TEXT = {
  login: { title: "Log in", submit: "Log in", other: "New here?", otherLink: "Create an account", otherPath: "/register" },
  register: { title: "Create account", submit: "Create account", other: "Already have an account?", otherLink: "Log in", otherPath: "/login" },
};

const INPUT = "mt-2 block w-full rounded-md border border-slate-300 px-3 py-1.5 font-normal outline-none focus:border-brand focus:ring-2 focus:ring-brand/20";
const LABEL = "block";

export default function AuthPage({ mode }: { mode: "login" | "register" }) {
  const [params] = useSearchParams();
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [accepted, setAccepted] = useState(false);
  const text = TEXT[mode];
  const query = params.toString();
  const registering = mode === "register";

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const email = String(form.get("email"));
    const password = String(form.get("password"));
    if (registering && password !== form.get("confirm")) {
      setError("Passwords do not match");
      return;
    }

    setBusy(true);
    setError("");
    try {
      if (registering) await api.register({ name: String(form.get("name")), email, password });
      else await api.login({ email, password });

      const productId = params.get("buy");
      if (productId) {
        const order = await api.createOrder(productId);
        openCabinet(`orders?pay=${order.id}`);
      } else {
        window.location.assign(localPath(params.get("next")));
      }
    } catch (failure) {
      setError(errorMessage(failure));
      setBusy(false);
    }
  }

  return (
    <section className="mx-auto max-w-[402px] px-4 py-12">
      <div className="mb-6 text-center">
        <h1 className="text-3xl font-medium">{text.title}</h1>
      </div>
      <div className="rounded-md border border-slate-200 bg-white p-6 shadow-sm">
      <form onSubmit={submit} className="flex flex-col gap-4">
        {error && (
          <p role="alert" className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">
            {error}
          </p>
        )}
        {registering && (
          <label className={LABEL}>
            Full Name
            <input name="name" required maxLength={100} autoComplete="name" placeholder="e.g. Aziz Karimov" className={INPUT} />
          </label>
        )}
        <label className={LABEL}>
          Email
          <input name="email" type="email" required autoComplete="email" placeholder="name@example.com" className={INPUT} />
        </label>
        <label className={LABEL}>
          Password
          <input
            name="password"
            type="password"
            required
            minLength={6}
            maxLength={72}
            autoComplete={registering ? "new-password" : "current-password"}
            placeholder={registering ? "At least 6 characters" : "Your password"}
            className={INPUT}
          />
        </label>
        {registering && (
          <>
            <label className={LABEL}>
              Confirm password
              <input
                name="confirm"
                type="password"
                required
                maxLength={72}
                autoComplete="new-password"
                placeholder="Repeat the password"
                className={INPUT}
              />
            </label>
            <label className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={accepted}
                onChange={(event) => setAccepted(event.target.checked)}
                className="h-4 w-4 accent-brand"
              />
              I accept the terms of the agreement
            </label>
          </>
        )}
        <button
          type="submit"
          disabled={busy || (registering && !accepted)}
          className="w-full cursor-pointer rounded-md bg-brand px-4 py-1.5 text-white hover:bg-brand-dark disabled:cursor-not-allowed disabled:opacity-65"
        >
          {text.submit}
        </button>
      </form>
      <p className="mt-4 text-center">
        {text.other}{" "}
        <Link to={`${text.otherPath}${query ? `?${query}` : ""}`} className="text-brand hover:text-brand-dark">
          {text.otherLink}
        </Link>
      </p>
      </div>
    </section>
  );
}
