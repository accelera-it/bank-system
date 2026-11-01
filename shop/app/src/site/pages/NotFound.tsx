import { Link } from "react-router-dom";

export default function NotFound() {
  return (
    <section className="mx-auto max-w-6xl px-4 py-20 text-center">
      <h1 className="text-3xl font-bold tracking-tight">Page not found</h1>
      <Link to="/" className="mt-4 inline-block font-semibold text-brand-text hover:text-brand-text">
        Back to the shop
      </Link>
    </section>
  );
}
