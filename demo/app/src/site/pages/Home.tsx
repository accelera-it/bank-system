import { Link } from "react-router-dom";

import ProductGrid from "../components/ProductGrid";

const BENEFITS = [
  ["Pay from your bank", "Confirm the payment in your bank's own page. The shop never sees your login."],
  ["Or pay by card", "Enter your card details at checkout and the order is paid right away."],
  ["Track your orders", "Every order and its payment status stay in your cabinet."],
];

export default function Home() {
  return (
    <>
      <section className="bg-brand-soft">
        <div className="mx-auto flex max-w-6xl flex-col items-start gap-6 px-4 py-16 sm:py-24">
          <h1 className="max-w-2xl text-4xl font-bold tracking-tight sm:text-5xl">Everything you need, one click away</h1>
          <p className="max-w-xl text-lg text-slate-600">Choose a product, pay safely through your bank and follow your order from the cabinet.</p>
          <Link to="/products" className="rounded-lg bg-brand px-6 py-3 font-semibold text-white hover:bg-brand-dark">
            Shop now
          </Link>
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-4 py-14">
        <div className="mb-6 flex items-end justify-between gap-4">
          <h2 className="text-2xl font-bold tracking-tight">Popular products</h2>
          <Link to="/products" className="text-sm font-semibold text-brand hover:text-brand-dark">
            View all
          </Link>
        </div>
        <ProductGrid limit={3} />
      </section>

      <section className="border-t border-slate-200 bg-slate-50">
        <div className="mx-auto grid max-w-6xl gap-8 px-4 py-14 sm:grid-cols-3">
          {BENEFITS.map(([title, text]) => (
            <div key={title}>
              <h3 className="font-semibold">{title}</h3>
              <p className="mt-2 text-sm text-slate-600">{text}</p>
            </div>
          ))}
        </div>
      </section>
    </>
  );
}
