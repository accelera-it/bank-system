import type { Order, Product, User } from "./types";

export class ApiError extends Error {
  status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

export async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const isForm = body instanceof FormData;
  const response = await fetch(`/api${path}`, {
    method,
    headers: body && !isForm ? { "Content-Type": "application/json" } : undefined,
    body: isForm ? body : body ? JSON.stringify(body) : undefined,
  });
  const data = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    const detail = typeof data?.detail === "string" ? data.detail : "Please check the form and try again.";
    throw new ApiError(response.status, detail);
  }
  return data as T;
}

export const api = {
  products: () => request<Product[]>("GET", "/products"),
  product: (id: string) => request<Product>("GET", `/products/${id}`),
  me: () => request<User>("GET", "/auth/me"),
  createOrder: (productId: string) => request<Order>("POST", "/orders", { product_id: productId }),
};

export function formatPrice(price: number): string {
  return `${new Intl.NumberFormat("en-US").format(price).replaceAll(",", " ")} UZS`;
}

/** Full page load, so the cabinet starts with the new order. */
export function openCabinet(path = ""): void {
  window.location.assign(`/cabinet/${path}`);
}


export function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : "Something went wrong. Please try again.";
}
