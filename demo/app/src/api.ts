import type { Credentials, Order, Product, Registration, User } from "./types";

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
  login: (credentials: Credentials) => request<User>("POST", "/auth/login", credentials),
  register: (registration: Registration) => request<User>("POST", "/auth/register", registration),
  logout: () => request<null>("POST", "/auth/logout"),
  createOrder: (productId: string) => request<Order>("POST", "/orders", { product_id: productId }),
};

export function formatPrice(price: number): string {
  return `${new Intl.NumberFormat("en-US").format(price).replaceAll(",", " ")} UZS`;
}

/** Full page load, so the header and the cabinet start from the new session. */
export function openCabinet(path = ""): void {
  window.location.assign(`/cabinet/${path}`);
}

/** Keeps a ?next= value inside this site. */
export function localPath(next: string | null): string {
  return next?.startsWith("/") && !next.startsWith("//") ? next : "/cabinet";
}

export function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : "Something went wrong. Please try again.";
}
