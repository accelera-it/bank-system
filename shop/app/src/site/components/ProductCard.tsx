import { formatPrice } from "../../api";
import type { Product } from "../../types";
import { useBuy } from "../useBuy";

export default function ProductCard({ product }: { product: Product }) {
  const buy = useBuy();

  return (
    <article className="flex flex-col overflow-hidden rounded-2xl border border-slate-200 transition hover:shadow-lg">
      <div className="flex aspect-[4/3] items-center justify-center bg-slate-50 p-6">
        <img src={product.image_url} alt={product.name} className="max-h-full max-w-full object-contain" />
      </div>
      <div className="flex flex-1 flex-col gap-3 p-5">
        <h3 className="font-medium">{product.name}</h3>
        <div className="mt-auto flex items-center justify-between gap-3">
          <span className="font-bold">{formatPrice(product.price)}</span>
          <button
            type="button"
            onClick={() => buy(product.id)}
            className="cursor-pointer rounded-lg bg-brand px-3 py-2 text-sm font-semibold text-slate-900 hover:bg-brand-dark"
          >
            Buy
          </button>
        </div>
      </div>
    </article>
  );
}
