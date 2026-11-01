import { ApiError, api, openCabinet } from "../api";

/** Creates an order and opens its payment in the cabinet; guests sign in first. */
export function useBuy() {
  return async (productId: string) => {
    try {
      const order = await api.createOrder(productId);
      openCabinet(`orders?pay=${order.id}`);
    } catch (error) {
      if (!(error instanceof ApiError) || error.status !== 401) throw error;
      window.location.assign(`/api/auth/login?buy=${productId}`);
    }
  };
}
