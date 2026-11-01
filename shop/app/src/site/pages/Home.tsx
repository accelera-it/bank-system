import type { ReactNode } from "react";
import { Link } from "react-router-dom";

const ICONS: Record<string, ReactNode> = {
  bag: (
    <>
      <path d="M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4Z" />
      <path d="M3 6h18M16 10a4 4 0 0 1-8 0" />
    </>
  ),
  arrow: <path d="M5 12h14M13 6l6 6-6 6" />,
  bank: (
    <>
      <path d="m3 10 9-6 9 6" />
      <path d="M5 10v8M10 10v8M14 10v8M19 10v8M3 21h18" />
    </>
  ),
  card: (
    <>
      <rect x="2" y="5" width="20" height="14" rx="2" />
      <path d="M2 10h20M6 15h4" />
    </>
  ),
  shield: (
    <>
      <path d="M12 3 5 6v6c0 4.5 3 7.5 7 9 4-1.5 7-4.5 7-9V6z" />
      <path d="m9 12 2 2 4-4" />
    </>
  ),
  money: (
    <>
      <rect x="2" y="6" width="20" height="12" rx="2" />
      <circle cx="12" cy="12" r="2.5" />
    </>
  ),
  orders: (
    <>
      <path d="M9 4h6v3H9z" />
      <path d="M7 5H5v16h14V5h-2M9 12h6M9 16h6" />
    </>
  ),
  clock: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 7v5l3 2" />
    </>
  ),
  box: (
    <>
      <path d="m12 3 8 4.500v9L12 21l-8-4.500v-9z" />
      <path d="m4 7.500 8 4.500 8-4.500M12 12v9" />
    </>
  ),
};

function Icon({ name, className = "h-5 w-5 text-brand-text" }: { name: string; className?: string }) {
  return (
    <svg className={className} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      {ICONS[name]}
    </svg>
  );
}

const HIGHLIGHTS = [
  ["bank", "Pay from your bank"],
  ["card", "Pay by card"],
  ["shield", "One secure sign-in"],
  ["money", "Prices in UZS"],
];

const FEATURES = [
  ["bank", "Pay from your bank", "Confirm the payment on your bank's own page. The shop never sees your login."],
  ["card", "Pay by card", "Enter your card details at checkout and the order is paid right away."],
  ["shield", "One secure sign-in", "Use one account for the shop and the bank, and sign in only once."],
  ["orders", "Track your orders", "Every order and its payment status stay in your cabinet."],
  ["clock", "Finish later", "An unpaid order waits in your cabinet until you are ready to pay."],
  ["box", "Manage products", "Add products with their pictures and keep prices up to date in the cabinet."],
];

const STEPS = [
  ["Choose a product", "Browse the catalog and press Buy on what you need."],
  ["Sign in once", "Use your central account, or create one in a minute."],
  ["Pay your way", "Pay from your bank account or by card, in a few clicks."],
];

export default function Home() {
  return (
    <>
      <section className="bg-gradient-to-r from-brand-soft via-[#fdf3d0] to-[#ffe27d]">
        <div className="mx-auto flex max-w-6xl items-center justify-between gap-10 px-4 py-16 sm:py-24">
          <div className="flex flex-col items-start gap-6">
            <h1 className="max-w-2xl text-4xl font-bold tracking-tight sm:text-5xl">Everything you need, one click away</h1>
            <p className="max-w-xl text-lg text-slate-700">Choose a product, pay safely through your bank and follow your order from the cabinet.</p>
            <Link to="/products" className="flex items-center gap-2 rounded-lg bg-brand px-6 py-3 font-semibold text-slate-900 hover:bg-brand-dark">
              <Icon name="bag" className="h-5 w-5" />
              Shop now
            </Link>
          </div>
          <img src="/img/logo.svg" alt="" className="hidden h-64 w-64 shrink-0 md:block" />
        </div>
      </section>

      <section className="border-b border-slate-200 bg-white">
        <ul className="mx-auto grid max-w-6xl gap-4 px-4 py-5 text-center font-semibold sm:grid-cols-2 lg:grid-cols-4">
          {HIGHLIGHTS.map(([icon, text]) => (
            <li key={text} className="flex items-center justify-center gap-2">
              <Icon name={icon} />
              {text}
            </li>
          ))}
        </ul>
      </section>

      <section className="mx-auto max-w-6xl px-4 py-16">
        <h2 className="text-center text-3xl font-bold tracking-tight">Everything your shopping needs, in one place</h2>
        <div className="mt-10 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {FEATURES.map(([icon, title, text]) => (
            <article key={title} className="rounded-2xl border border-slate-200 p-6">
              <span className="mb-4 flex h-12 w-12 items-center justify-center rounded-xl bg-[#fdf3d0]">
                <Icon name={icon} className="h-6 w-6 text-brand-text" />
              </span>
              <h3 className="font-semibold">{title}</h3>
              <p className="mt-2 text-sm text-slate-600">{text}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="bg-brand-soft">
        <div className="mx-auto max-w-6xl px-4 py-16">
          <h2 className="text-center text-3xl font-bold tracking-tight">Up and running in three steps</h2>
          <ol className="mt-10 grid gap-8 sm:grid-cols-3">
            {STEPS.map(([title, text], index) => (
              <li key={title}>
                <span className="flex h-10 w-10 items-center justify-center rounded-full bg-brand font-bold">{index + 1}</span>
                <h3 className="mt-4 font-semibold">{title}</h3>
                <p className="mt-2 text-sm text-slate-600">{text}</p>
              </li>
            ))}
          </ol>
        </div>
      </section>

      <section className="bg-gradient-to-r from-brand to-[#ffe27d]">
        <div className="mx-auto flex max-w-6xl flex-col items-center gap-5 px-4 py-14 text-center">
          <h2 className="text-3xl font-bold tracking-tight">Ready to order?</h2>
          <Link to="/products" className="flex items-center gap-2 rounded-lg border border-slate-900 px-6 py-3 font-semibold hover:bg-slate-900 hover:text-white">
            Browse products
            <Icon name="arrow" className="h-5 w-5" />
          </Link>
        </div>
      </section>
    </>
  );
}
