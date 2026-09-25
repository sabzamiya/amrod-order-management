import {
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import {
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from "vitest";

import OrdersPage from "./OrdersPage";
import { getCustomers } from "../api/customers";
import {
  createOrder,
  getOrders,
  updateOrderStatus,
} from "../api/orders";

vi.mock("../api/customers", () => ({
  getCustomers: vi.fn(),
  createCustomer: vi.fn(),
}));

vi.mock("../api/orders", () => ({
  getOrders: vi.fn(),
  createOrder: vi.fn(),
  updateOrderStatus: vi.fn(),
}));

const mockedGetCustomers =
  vi.mocked(getCustomers);

const mockedGetOrders =
  vi.mocked(getOrders);

const mockedCreateOrder =
  vi.mocked(createOrder);

const mockedUpdateOrderStatus =
  vi.mocked(updateOrderStatus);

const customer = {
  id: "customer-1",
  name: "Sabelo Trading",
  email: "sabelo@example.com",
  countryCode: "ZA",
  createdAt: "2026-09-24T10:00:00Z",
};

const pendingOrder = {
  id: "12345678-1111-2222-3333-444444444444",
  customerId: customer.id,
  countryCode: "ZA",
  currencyCode: "ZAR",
  status: "Pending" as const,
  totalAmount: 350,
  createdAt: "2026-09-24T10:00:00Z",
  lineItems: [],
};

describe("OrdersPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();

    mockedGetCustomers.mockResolvedValue({
      items: [customer],
      page: 1,
      pageSize: 100,
      totalCount: 1,
    });

    mockedGetOrders.mockResolvedValue({
      items: [pendingOrder],
      page: 1,
      pageSize: 10,
      totalCount: 1,
    });

    mockedCreateOrder.mockResolvedValue(
      pendingOrder
    );

    mockedUpdateOrderStatus.mockResolvedValue({
      ...pendingOrder,
      status: "Paid",
    });
  });

  it("loads and displays orders", async () => {
    render(<OrdersPage />);

    expect(
      screen.getByText(/Loading orders/i)
    ).toBeInTheDocument();

    expect(
      await screen.findByText("12345678...")
    ).toBeInTheDocument();

    expect(
      screen.getByText("Sabelo Trading")
    ).toBeInTheDocument();

    expect(
      screen.getByText("ZAR 350.00")
    ).toBeInTheDocument();

    expect(
      screen.getByText("Pending", {
        selector: ".status-badge",
      })
    ).toBeInTheDocument();

    expect(
      mockedGetOrders
    ).toHaveBeenCalledWith(
      1,
      10,
      undefined,
      "createdAt",
      true
    );

    expect(
      mockedGetCustomers
    ).toHaveBeenCalledWith(
      "",
      1,
      100
    );
  });

  it("filters orders by status", async () => {
    const user = userEvent.setup();

    mockedGetOrders
      .mockResolvedValueOnce({
        items: [pendingOrder],
        page: 1,
        pageSize: 10,
        totalCount: 1,
      })
      .mockResolvedValueOnce({
        items: [
          {
            ...pendingOrder,
            status: "Paid",
          },
        ],
        page: 1,
        pageSize: 10,
        totalCount: 1,
      });

    render(<OrdersPage />);

    await screen.findByText("12345678...");

    const statusFilter =
      screen.getByRole("combobox", {
        name: /filter orders by status/i,
      });

    await user.selectOptions(
      statusFilter,
      "Paid"
    );

    await waitFor(() => {
      expect(
        mockedGetOrders
      ).toHaveBeenLastCalledWith(
        1,
        10,
        "Paid",
        "createdAt",
        true
      );
    });

    expect(
      await screen.findByText("Paid", {
        selector: ".status-badge",
      })
    ).toBeInTheDocument();
  });

  it(
    "allows a pending order to be marked as paid",
    async () => {
      const user = userEvent.setup();

      mockedGetOrders
        .mockResolvedValueOnce({
          items: [pendingOrder],
          page: 1,
          pageSize: 10,
          totalCount: 1,
        })
        .mockResolvedValueOnce({
          items: [
            {
              ...pendingOrder,
              status: "Paid",
            },
          ],
          page: 1,
          pageSize: 10,
          totalCount: 1,
        });

      render(<OrdersPage />);

      await screen.findByText("12345678...");

      await user.click(
        screen.getByRole("button", {
          name: /mark paid/i,
        })
      );

      await waitFor(() => {
        expect(
          mockedUpdateOrderStatus
        ).toHaveBeenCalledWith(
          pendingOrder.id,
          "Paid"
        );
      });

      expect(
        await screen.findByText(
          /Order status updated to Paid/i
        )
      ).toBeInTheDocument();
    }
  );

  it(
    "shows completed orders without status action buttons",
    async () => {
      mockedGetOrders.mockResolvedValue({
        items: [
          {
            ...pendingOrder,
            status: "Fulfilled",
          },
        ],
        page: 1,
        pageSize: 10,
        totalCount: 1,
      });

      render(<OrdersPage />);

      expect(
        await screen.findByText("Fulfilled", {
          selector: ".status-badge",
        })
      ).toBeInTheDocument();

      expect(
        screen.getByText("Complete")
      ).toBeInTheDocument();

      expect(
        screen.queryByRole("button", {
          name: /mark paid/i,
        })
      ).not.toBeInTheDocument();

      expect(
        screen.queryByRole("button", {
          name: /fulfill/i,
        })
      ).not.toBeInTheDocument();
    }
  );
});