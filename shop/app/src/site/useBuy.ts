import { useNavigate } from "react-router-dom";

import { ApiError, api, openCabinet } from "../api";

/** Creates an order and opens its payment in the cabinet; guests sign in first. */
export function useBuy() {
  const navigate = useNavigate();

  return async (productId: string) => {
    try {
      const order = await api.createOrder(productId);
      openCabinet(`orders?pay=${order.id}`);
    } catch (error) {
      if (!(error instanceof ApiError) || error.status !== 401) throw error;
      navigate(`/login?buy=${productId}`);
    }
  };
}
