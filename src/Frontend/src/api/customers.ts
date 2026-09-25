import { apiRequest } from "./client";
import type {
  CreateCustomerRequest,
  Customer,
  PagedResponse,
} from "../types";

export function getCustomers(
  search = "",
  page = 1,
  pageSize = 10
) {
  const params = new URLSearchParams({
    page: page.toString(),
    pageSize: pageSize.toString(),
  });

  if (search) {
    params.set("search", search);
  }

  return apiRequest<PagedResponse<Customer>>(
    `/customers?${params}`
  );
}

export function createCustomer(
  request: CreateCustomerRequest
) {
  return apiRequest<Customer>("/customers", {
    method: "POST",
    body: JSON.stringify(request),
  });
}