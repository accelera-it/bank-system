import { useEffect, useState } from "react";

import { api } from "../../api";
import type { Product } from "../../types";
import ProductCard from "./ProductCard";

export default function ProductGrid() {
  const [products, setProducts] = useState<Product[] | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    api.products().then(setProducts).catch(() => setFailed(true));
  }, []);

  if (failed) return <p className="text-slate-500">Products could not be loaded. Please try again later.</p>;
  if (!products) return <p className="text-slate-500">Loading…</p>;
  if (products.length === 0) return <p className="text-slate-500">There are no products yet.</p>;

  return (
    <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
      {products.map((product) => (
        <ProductCard key={product.id} product={product} />
      ))}
    </div>
  );
}
