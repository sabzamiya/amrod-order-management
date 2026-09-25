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

import CustomersPage from "./CustomersPage";
import {
  createCustomer,
  getCustomers,
} from "../api/customers";

vi.mock("../api/customers", () => ({
  getCustomers: vi.fn(),
  createCustomer: vi.fn(),
}));

const mockedGetCustomers =
  vi.mocked(getCustomers);

const mockedCreateCustomer =
  vi.mocked(createCustomer);

describe("CustomersPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();

    mockedCreateCustomer.mockResolvedValue({
      id: "customer-created",
      name: "Created Customer",
      email: "created@example.com",
      countryCode: "ZA",
      createdAt: "2026-09-24T10:00:00Z",
    });
  });

  it("loads and displays customers", async () => {
    mockedGetCustomers.mockResolvedValue({
      items: [
        {
          id: "customer-1",
          name: "Sabelo Trading",
          email: "sabelo@example.com",
          countryCode: "ZA",
          createdAt: "2026-09-24T10:00:00Z",
        },
      ],
      page: 1,
      pageSize: 10,
      totalCount: 1,
    });

    render(<CustomersPage />);

    expect(
      screen.getByText(/Loading customers/i)
    ).toBeInTheDocument();

    expect(
      await screen.findByText("Sabelo Trading")
    ).toBeInTheDocument();

    expect(
      screen.getByText("sabelo@example.com")
    ).toBeInTheDocument();

    expect(
      screen.getByText("ZA")
    ).toBeInTheDocument();

    expect(mockedGetCustomers).toHaveBeenCalledWith(
      "",
      1,
      10
    );
  });

  it("shows an empty state when no customers exist", async () => {
    mockedGetCustomers.mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 10,
      totalCount: 0,
    });

    render(<CustomersPage />);

    expect(
      await screen.findByText(
        "No customers found."
      )
    ).toBeInTheDocument();
  });

  it("searches customers by the entered search value", async () => {
    const user = userEvent.setup();

    mockedGetCustomers
      .mockResolvedValueOnce({
        items: [],
        page: 1,
        pageSize: 10,
        totalCount: 0,
      })
      .mockResolvedValueOnce({
        items: [
          {
            id: "customer-2",
            name: "Amrod Test Customer",
            email: "amrod@example.com",
            countryCode: "BW",
            createdAt:
              "2026-09-24T10:00:00Z",
          },
        ],
        page: 1,
        pageSize: 10,
        totalCount: 1,
      });

    render(<CustomersPage />);

    await screen.findByText(
      "No customers found."
    );

    const searchInput =
      screen.getByRole("searchbox", {
        name: /search customers/i,
      });

    await user.type(
      searchInput,
      "Amrod"
    );

    await user.click(
      screen.getByRole("button", {
        name: /^search$/i,
      })
    );

    await waitFor(() => {
      expect(
        mockedGetCustomers
      ).toHaveBeenLastCalledWith(
        "Amrod",
        1,
        10
      );
    });

    expect(
      await screen.findByText(
        "Amrod Test Customer"
      )
    ).toBeInTheDocument();
  });

  it("loads the next page of customers", async () => {
    const user = userEvent.setup();

    mockedGetCustomers
      .mockResolvedValueOnce({
        items: [
          {
            id: "customer-page-1",
            name: "Page One Customer",
            email: "page1@example.com",
            countryCode: "ZA",
            createdAt:
              "2026-09-24T10:00:00Z",
          },
        ],
        page: 1,
        pageSize: 10,
        totalCount: 20,
      })
      .mockResolvedValueOnce({
        items: [
          {
            id: "customer-page-2",
            name: "Page Two Customer",
            email: "page2@example.com",
            countryCode: "ZA",
            createdAt:
              "2026-09-24T10:00:00Z",
          },
        ],
        page: 2,
        pageSize: 10,
        totalCount: 20,
      });

    render(<CustomersPage />);

    expect(
      await screen.findByText(
        "Page One Customer"
      )
    ).toBeInTheDocument();

    await user.click(
      screen.getByRole("button", {
        name: /next/i,
      })
    );

    await waitFor(() => {
      expect(
        mockedGetCustomers
      ).toHaveBeenLastCalledWith(
        "",
        2,
        10
      );
    });

    expect(
      await screen.findByText(
        "Page Two Customer"
      )
    ).toBeInTheDocument();
  });
});