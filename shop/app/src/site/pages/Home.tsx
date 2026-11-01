import { Link } from "react-router-dom";

const HIGHLIGHTS = ["Pay from your bank", "Pay by card", "One secure sign-in", "Prices in UZS"];

const FEATURES = [
  ["Pay from your bank", "Confirm the payment on your bank's own page. The shop never sees your login."],
  ["Pay by card", "Enter your card details at checkout and the order is paid right away."],
  ["One secure sign-in", "Use one account for the shop and the bank, and sign in only once."],
  ["Track your orders", "Every order and its payment status stay in your cabinet."],
  ["Finish later", "An unpaid order waits in your cabinet until you are ready to pay."],
  ["Manage products", "Add products with their pictures and keep prices up to date in the cabinet."],
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
        <div className="mx-auto flex max-w-6xl flex-col items-start gap-6 px-4 py-16 sm:py-24">
          <h1 className="max-w-2xl text-4xl font-bold tracking-tight sm:text-5xl">Everything you need, one click away</h1>
          <p className="max-w-xl text-lg text-slate-700">Choose a product, pay safely through your bank and follow your order from the cabinet.</p>
          <Link to="/products" className="rounded-lg bg-brand px-6 py-3 font-semibold text-slate-900 hover:bg-brand-dark">
            Shop now
          </Link>
        </div>
      </section>

      <section className="border-b border-slate-200 bg-white">
        <ul className="mx-auto grid max-w-6xl gap-4 px-4 py-5 text-center font-semibold sm:grid-cols-2 lg:grid-cols-4">
          {HIGHLIGHTS.map((text) => (
            <li key={text}>{text}</li>
          ))}
        </ul>
      </section>

      <section className="mx-auto max-w-6xl px-4 py-16">
        <h2 className="text-center text-3xl font-bold tracking-tight">Everything your shopping needs, in one place</h2>
        <div className="mt-10 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {FEATURES.map(([title, text]) => (
            <article key={title} className="rounded-2xl border border-slate-200 p-6">
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
          <Link to="/products" className="rounded-lg border border-slate-900 px-6 py-3 font-semibold hover:bg-slate-900 hover:text-white">
            Browse products
          </Link>
        </div>
      </section>
    </>
  );
}
