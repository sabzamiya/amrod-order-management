import { apiRequest } from "./client";

import type {
  CreateOrderRequest,
  Order,
  OrderStatus,
  PagedResponse,
} from "../types";

export type OrderSortBy =
  | "createdAt"
  | "totalAmount";

export function getOrders(
  page = 1,
  pageSize = 10,
  status?: OrderStatus,
  sortBy: OrderSortBy = "createdAt",
  descending = true
) {
  const params =
    new URLSearchParams({
      page: page.toString(),
      pageSize: pageSize.toString(),
      sortBy,
      descending:
        descending.toString(),
    });

  if (status) {
    params.set(
      "status",
      status
    );
  }

  return apiRequest<
    PagedResponse<Order>
  >(
    `/orders?${params.toString()}`
  );
}

export function getOrder(
  id: string
) {
  return apiRequest<Order>(
    `/orders/${id}`
  );
}

export function createOrder(
  request: CreateOrderRequest
) {
  return apiRequest<Order>(
    "/orders",
    {
      method: "POST",
      body: JSON.stringify(
        request
      ),
    }
  );
}

export function updateOrderStatus(
  id: string,
  status: OrderStatus
) {
  return apiRequest<Order>(
    `/orders/${id}/status`,
    {
      method: "PUT",

      headers: {
        "Idempotency-Key":
          crypto.randomUUID(),
      },

      body: JSON.stringify({
        status,
      }),
    }
  );
}