import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";

import { api, formatPrice } from "../../api";
import type { Product } from "../../types";
import { useBuy } from "../useBuy";
import NotFound from "./NotFound";

export default function ProductDetails() {
  const { id } = useParams() as { id: string };
  const buy = useBuy();
  const [product, setProduct] = useState<Product | null>(null);
  const [missing, setMissing] = useState(false);

  useEffect(() => {
    api.product(id).then(setProduct).catch(() => setMissing(true));
  }, [id]);

  if (missing) return <NotFound />;
  if (!product) return <p className="mx-auto max-w-6xl px-4 py-12 text-slate-500">Loading…</p>;

  return (
    <section className="mx-auto grid max-w-6xl gap-10 px-4 py-12 md:grid-cols-2">
      <div className="flex aspect-[4/3] items-center justify-center rounded-2xl bg-slate-50 p-8">
        <img src={product.image_url} alt={product.name} className="max-h-full max-w-full object-contain" />
      </div>
      <div className="flex flex-col items-start gap-5">
        <Link to="/products" className="text-sm font-semibold text-brand-text hover:text-brand-text">
          ← All products
        </Link>
        <h1 className="text-3xl font-bold tracking-tight">{product.name}</h1>
        <p className="text-2xl font-bold">{formatPrice(product.price)}</p>
        <button
          type="button"
          onClick={() => buy(product.id)}
          className="cursor-pointer rounded-lg bg-brand px-6 py-3 font-semibold text-slate-900 hover:bg-brand-dark"
        >
          Buy now
        </button>
      </div>
    </section>
  );
}
