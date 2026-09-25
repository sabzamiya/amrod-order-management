export type OrderStatus =
  | "Pending"
  | "Paid"
  | "Fulfilled"
  | "Cancelled";

export interface Customer {
  id: string;
  name: string;
  email: string;
  countryCode: string;
  createdAt: string;
}

export interface OrderLineItem {
  id: string;
  productCode: string;
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface Order {
  id: string;
  customerId: string;
  countryCode: string;
  currencyCode: string;
  status: OrderStatus;
  totalAmount: number;
  createdAt: string;
  updatedAt?: string;
  lineItems: OrderLineItem[];
}

export interface CreateCustomerRequest {
  name: string;
  email: string;
  countryCode: string;
}

export interface CreateOrderLineItemRequest {
  productCode: string;
  description: string;
  quantity: number;
  unitPrice: number;
}

export interface CreateOrderRequest {
  customerId: string;
  countryCode: string;
  currencyCode: string;
  lineItems: CreateOrderLineItemRequest[];
}

export interface PagedResponse<T> {
  page: number;
  pageSize: number;
  totalCount: number;
  items: T[];
}