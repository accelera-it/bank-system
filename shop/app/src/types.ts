export interface Profile {
  name: string;
  phone: string;
  address: string;
}

export interface User extends Profile {
  id: string;
  email: string;
}

export interface Product {
  id: string;
  name: string;
  price: number;
  image_url: string;
}

export type ProductInput = Omit<Product, "id">;

export type PaymentStatus = "pending" | "completed";

export interface Order {
  id: string;
  product: Product;
  payment_status: PaymentStatus;
  created_at: string;
}

export interface Card {
  number: string;
  name: string;
  expiry_date: string;
  security_code: string;
}

export interface DirectPayment {
  url: string;
  field: string;
  data: string;
}
