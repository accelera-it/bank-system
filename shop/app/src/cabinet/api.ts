import { ApiError, request } from "../api";
import type { Card, DirectPayment, Order, Product, ProductInput, User } from "../types";

export { errorMessage, formatPrice } from "../api";

async function signedIn<T>(method: string, path: string, body?: unknown): Promise<T> {
  try {
    return await request<T>(method, path, body);
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      const next = encodeURIComponent(window.location.pathname + window.location.search);
      window.location.assign(`/api/auth/login?next=${next}`);
    }
    throw error;
  }
}

export const api = {
  me: () => signedIn<User>("GET", "/auth/me"),
  logout: () => signedIn<{ url: string }>("POST", "/auth/logout"),
  uploadImage: (file: File) => {
    const form = new FormData();
    form.append("file", file);
    return signedIn<{ url: string }>("POST", "/products/images", form);
  },
  products: () => signedIn<Product[]>("GET", "/products"),
  createProduct: (product: ProductInput) => signedIn<Product>("POST", "/products", product),
  updateProduct: (id: string, product: ProductInput) => signedIn<Product>("PUT", `/products/${id}`, product),
  deleteProduct: (id: string) => signedIn<null>("DELETE", `/products/${id}`),
  orders: () => signedIn<Order[]>("GET", "/orders"),
  directPayment: (orderId: string) => signedIn<DirectPayment>("POST", `/orders/${orderId}/direct-payment`),
  cardPayment: (orderId: string, card: Card) => signedIn<Order>("POST", `/orders/${orderId}/card-payment`, card),
};

/** Sends the browser to CentralApi with the signed payment request. */
export function postRedirect({ url, field, data }: DirectPayment): void {
  const form = document.createElement("form");
  form.method = "post";
  form.action = url;
  const input = document.createElement("input");
  input.type = "hidden";
  input.name = field;
  input.value = data;
  form.append(input);
  document.body.append(form);
  form.submit();
}
