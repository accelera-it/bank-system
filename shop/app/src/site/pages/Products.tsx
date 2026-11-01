import ProductGrid from "../components/ProductGrid";

export default function Products() {
  return (
    <section className="mx-auto max-w-6xl px-4 py-12">
      <h1 className="mb-8 text-3xl font-bold tracking-tight">Products</h1>
      <ProductGrid />
    </section>
  );
}
