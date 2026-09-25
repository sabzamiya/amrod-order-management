import {
  useEffect,
  useMemo,
  useState,
} from "react";
import type { FormEvent } from "react";
import {
  AnimatePresence,
  motion,
} from "framer-motion";

import { getCustomers } from "../api/customers";

import {
  createOrder,
  getOrder,
  getOrders,
  updateOrderStatus,
} from "../api/orders";
import type { OrderSortBy } from "../api/orders";

import type {
  Customer,
  Order,
  OrderStatus,
} from "../types";

const PAGE_SIZE = 10;
const MINIMUM_LOADING_TIME = 600;

const CURRENCY_BY_COUNTRY: Record<
  string,
  string[]
> = {
  AO: ["AOA"],
  BW: ["BWP"],
  KM: ["KMF"],
  CD: ["CDF"],
  SZ: ["SZL", "ZAR"],
  LS: ["LSL", "ZAR"],
  MG: ["MGA"],
  MW: ["MWK"],
  MU: ["MUR"],
  MZ: ["MZN"],
  NA: ["NAD", "ZAR"],
  SC: ["SCR"],
  ZA: ["ZAR"],
  TZ: ["TZS"],
  ZM: ["ZMW"],
  ZW: ["ZWL", "USD"],
};

type CreateLineItem = {
  productCode: string;
  description: string;
  quantity: number;
  unitPrice: number;
};

const EMPTY_LINE_ITEM: CreateLineItem = {
  productCode: "",
  description: "",
  quantity: 1,
  unitPrice: 0,
};

function OrdersPage() {
  const [orders, setOrders] =
    useState<Order[]>([]);

  const [customers, setCustomers] =
    useState<Customer[]>([]);

  const [page, setPage] =
    useState(1);

  const [totalCount, setTotalCount] =
    useState(0);

  const [statusFilter, setStatusFilter] =
    useState("");

  const [sortBy, setSortBy] =
    useState<OrderSortBy>("createdAt");

  const [descending, setDescending] =
    useState(true);

  const [loading, setLoading] =
    useState(true);

  const [creating, setCreating] =
    useState(false);

  const [
    updatingOrderId,
    setUpdatingOrderId,
  ] = useState<string | null>(null);

  const [error, setError] =
    useState("");

  const [
    successMessage,
    setSuccessMessage,
  ] = useState("");

  const [showForm, setShowForm] =
    useState(false);

  /*
   * Order Details state
   */
  const [
    selectedOrder,
    setSelectedOrder,
  ] = useState<Order | null>(null);

  const [
    loadingOrderDetails,
    setLoadingOrderDetails,
  ] = useState(false);

  const [
    orderDetailsError,
    setOrderDetailsError,
  ] = useState("");

  /*
   * Create Order state
   */
  const [customerId, setCustomerId] =
    useState("");

  const [countryCode, setCountryCode] =
    useState("ZA");

  const [currencyCode, setCurrencyCode] =
    useState("ZAR");

  const [lineItems, setLineItems] =
    useState<CreateLineItem[]>([
      { ...EMPTY_LINE_ITEM },
    ]);

  const totalPages = Math.max(
    1,
    Math.ceil(
      totalCount / PAGE_SIZE
    )
  );

  const estimatedTotal = useMemo(
    () =>
      lineItems.reduce(
        (total, item) =>
          total + item.quantity * item.unitPrice,
        0
      ),
    [lineItems]
  );

  const availableCurrencies =
    CURRENCY_BY_COUNTRY[
      countryCode
    ] ?? [];

  /*
   * Load orders
   */
  async function loadOrders(
    requestedPage = page,
    requestedStatus = statusFilter,
    showLoading = true,
    requestedSortBy = sortBy,
    requestedDescending = descending
  ) {
    try {
      if (showLoading) {
        setLoading(true);
      }

      setError("");

      const orderStatus =
        requestedStatus
          ? (requestedStatus as OrderStatus)
          : undefined;

      const result =
        await getOrders(
          requestedPage,
          PAGE_SIZE,
          orderStatus,
          requestedSortBy,
          requestedDescending
        );

      setOrders(result.items);

      setTotalCount(
        result.totalCount
      );
    } catch (loadError) {
      setError(
        loadError instanceof Error
          ? loadError.message
          : "Unable to load orders."
      );
    } finally {
      if (showLoading) {
        setLoading(false);
      }
    }
  }

  /*
   * Load customers
   */
  async function loadCustomers() {
    try {
      const result =
        await getCustomers(
          "",
          1,
          100
        );

      setCustomers(
        result.items
      );

      if (
        result.items.length > 0 &&
        !customerId
      ) {
        setCustomerId(
          result.items[0].id
        );
      }
    } catch (customerError) {
      setError(
        customerError instanceof Error
          ? customerError.message
          : "Unable to load customers."
      );
    }
  }

  /*
   * Initial page load
   */
  useEffect(() => {
    void Promise.all([
      loadOrders(
        1,
        "",
        true
      ),
      loadCustomers(),
    ]);
  }, []);

  /*
   * Keep currency valid when
   * country changes.
   */
  useEffect(() => {
    const currencies =
      CURRENCY_BY_COUNTRY[
        countryCode
      ];

    if (
      currencies &&
      currencies.length > 0 &&
      !currencies.includes(
        currencyCode
      )
    ) {
      setCurrencyCode(
        currencies[0]
      );
    }
  }, [
    countryCode,
    currencyCode,
  ]);

  /*
   * Filter orders
   */
  async function handleFilterChange(
    newStatus: string
  ) {
    setStatusFilter(
      newStatus
    );

    setPage(1);

    setError("");
    setSuccessMessage("");

    await loadOrders(
      1,
      newStatus,
      true,
      sortBy,
      descending
    );
  }

  async function handleSortByChange(
    newSortBy: OrderSortBy
  ) {
    setSortBy(newSortBy);
    setPage(1);
    setError("");
    setSuccessMessage("");

    await loadOrders(
      1,
      statusFilter,
      true,
      newSortBy,
      descending
    );
  }

  async function handleSortDirectionChange(
    newDescending: boolean
  ) {
    setDescending(newDescending);
    setPage(1);
    setError("");
    setSuccessMessage("");

    await loadOrders(
      1,
      statusFilter,
      true,
      sortBy,
      newDescending
    );
  }

  function updateLineItem(
    index: number,
    field: keyof CreateLineItem,
    value: string | number
  ) {
    setLineItems((current) =>
      current.map((item, itemIndex) =>
        itemIndex === index
          ? { ...item, [field]: value }
          : item
      )
    );
  }

  function addLineItem() {
    setLineItems((current) => [
      ...current,
      { ...EMPTY_LINE_ITEM },
    ]);
  }

  function removeLineItem(index: number) {
    setLineItems((current) =>
      current.length === 1
        ? current
        : current.filter(
            (_, itemIndex) => itemIndex !== index
          )
    );
  }

  const hasInvalidLineItems =
    lineItems.length === 0 ||
    lineItems.some(
      (item) =>
        !item.productCode.trim() ||
        !item.description.trim() ||
        item.quantity < 1 ||
        item.unitPrice < 0
    );

  /*
   * Create order
   */
  async function handleCreate(
    event: FormEvent<HTMLFormElement>
  ) {
    event.preventDefault();

    if (creating) {
      return;
    }

    setCreating(true);

    setError("");
    setSuccessMessage("");

    const loadingStartedAt =
      Date.now();

    try {
      await createOrder({
        customerId,
        countryCode,
        currencyCode,

        lineItems: lineItems.map((item) => ({
          productCode: item.productCode.trim(),
          description: item.description.trim(),
          quantity: item.quantity,
          unitPrice: item.unitPrice,
        })),
      });

      const elapsed =
        Date.now() -
        loadingStartedAt;

      if (
        elapsed <
        MINIMUM_LOADING_TIME
      ) {
        await new Promise<void>(
          (resolve) => {
            window.setTimeout(
              resolve,
              MINIMUM_LOADING_TIME -
                elapsed
            );
          }
        );
      }

      setLineItems([
        { ...EMPTY_LINE_ITEM },
      ]);

      setShowForm(false);
      setPage(1);

      setSuccessMessage(
        "Order created successfully. Processing is in progress."
      );

      /*
       * Immediate refresh.
       */
      await loadOrders(
        1,
        statusFilter,
        false
      );

      /*
       * RabbitMQ processing is
       * asynchronous, so refresh
       * again shortly afterwards.
       */
      window.setTimeout(() => {
        void loadOrders(
          1,
          statusFilter,
          false
        );
      }, 1000);

      window.setTimeout(() => {
        void loadOrders(
          1,
          statusFilter,
          false
        );
      }, 2500);

      window.setTimeout(() => {
        setSuccessMessage("");
      }, 4000);
    } catch (createError) {
      setError(
        createError instanceof Error
          ? createError.message
          : "Unable to create order."
      );
    } finally {
      setCreating(false);
    }
  }

  /*
   * Update order status
   */
  async function changeStatus(
    orderId: string,
    status: OrderStatus
  ) {
    if (updatingOrderId) {
      return;
    }

    setUpdatingOrderId(
      orderId
    );

    setError("");
    setSuccessMessage("");

    try {
      const updatedOrder =
        await updateOrderStatus(
          orderId,
          status
        );

      /*
       * Keep an open Order Details
       * modal synchronized.
       */
      if (
        selectedOrder?.id ===
        orderId
      ) {
        setSelectedOrder(
          updatedOrder
        );
      }

      setSuccessMessage(
        `Order status updated to ${status}.`
      );

      await loadOrders(
        page,
        statusFilter,
        false
      );

      window.setTimeout(() => {
        setSuccessMessage("");
      }, 3000);
    } catch {
      /*
       * The RabbitMQ worker may have
       * changed the order while the
       * browser was displaying an
       * older state.
       *
       * Reload authoritative state.
       */
      try {
        if (
          selectedOrder?.id ===
          orderId
        ) {
          const latestOrder =
            await getOrder(
              orderId
            );

          setSelectedOrder(
            latestOrder
          );
        }

        await loadOrders(
          page,
          statusFilter,
          false
        );

        setError(
          "The order status changed while you were viewing it. The latest status has been loaded."
        );
      } catch (refreshError) {
        setError(
          refreshError instanceof Error
            ? refreshError.message
            : "Unable to refresh the latest order status."
        );
      }
    } finally {
      setUpdatingOrderId(
        null
      );
    }
  }

  /*
   * Open Order Details
   */
  async function openOrderDetails(
    orderId: string
  ) {
    setLoadingOrderDetails(
      true
    );

    setOrderDetailsError("");

    try {
      const order =
        await getOrder(
          orderId
        );

      setSelectedOrder(
        order
      );
    } catch (detailsError) {
      setOrderDetailsError(
        detailsError instanceof Error
          ? detailsError.message
          : "Unable to load order details."
      );
    } finally {
      setLoadingOrderDetails(
        false
      );
    }
  }

  /*
   * Close Order Details
   */
  function closeOrderDetails() {
    if (updatingOrderId) {
      return;
    }

    setSelectedOrder(null);
    setOrderDetailsError("");
  }

  /*
   * Pagination
   */
  async function goToPreviousPage() {
    if (
      page <= 1 ||
      loading
    ) {
      return;
    }

    const previousPage =
      page - 1;

    setPage(
      previousPage
    );

    await loadOrders(
      previousPage,
      statusFilter,
      true
    );
  }

  async function goToNextPage() {
    if (
      page >= totalPages ||
      loading
    ) {
      return;
    }

    const nextPage =
      page + 1;

    setPage(
      nextPage
    );

    await loadOrders(
      nextPage,
      statusFilter,
      true
    );
  }

  /*
   * Create form
   */
  function toggleForm() {
    if (creating) {
      return;
    }

    setError("");
    setSuccessMessage("");

    setShowForm(
      (current) =>
        !current
    );
  }

  /*
   * Status transition actions
   */
  function renderOrderActions(
    order: Order
  ) {
    const updating =
      updatingOrderId ===
      order.id;

    if (
      order.status ===
        "Fulfilled" ||
      order.status ===
        "Cancelled"
    ) {
      return (
        <span className="muted">
          Complete
        </span>
      );
    }

    if (
      order.status ===
      "Pending"
    ) {
      return (
        <div className="action-buttons">
          <motion.button
            type="button"
            className="small-button"
            disabled={
              updating ||
              updatingOrderId !==
                null
            }
            onClick={() => {
              void changeStatus(
                order.id,
                "Paid"
              );
            }}
            whileTap={{
              scale: 0.96,
            }}
          >
            {updating ? (
              <span className="button-loading-content">
                <motion.span
                  className="button-spinner"
                  aria-hidden="true"
                  animate={{
                    rotate: 360,
                  }}
                  transition={{
                    duration: 0.75,
                    repeat: Infinity,
                    ease: "linear",
                  }}
                />

                Updating...
              </span>
            ) : (
              "Mark Paid"
            )}
          </motion.button>

          <motion.button
            type="button"
            className="small-button"
            disabled={
              updating ||
              updatingOrderId !==
                null
            }
            onClick={() => {
              void changeStatus(
                order.id,
                "Cancelled"
              );
            }}
            whileTap={{
              scale: 0.96,
            }}
          >
            Cancel
          </motion.button>
        </div>
      );
    }

    if (
      order.status ===
      "Paid"
    ) {
      return (
        <div className="action-buttons">
          <motion.button
            type="button"
            className="small-button"
            disabled={
              updating ||
              updatingOrderId !==
                null
            }
            onClick={() => {
              void changeStatus(
                order.id,
                "Fulfilled"
              );
            }}
            whileTap={{
              scale: 0.96,
            }}
          >
            {updating ? (
              <span className="button-loading-content">
                <motion.span
                  className="button-spinner"
                  aria-hidden="true"
                  animate={{
                    rotate: 360,
                  }}
                  transition={{
                    duration: 0.75,
                    repeat: Infinity,
                    ease: "linear",
                  }}
                />

                Updating...
              </span>
            ) : (
              "Fulfill"
            )}
          </motion.button>

          <motion.button
            type="button"
            className="small-button"
            disabled={
              updating ||
              updatingOrderId !==
                null
            }
            onClick={() => {
              void changeStatus(
                order.id,
                "Cancelled"
              );
            }}
            whileTap={{
              scale: 0.96,
            }}
          >
            Cancel
          </motion.button>
        </div>
      );
    }

    return (
      <span className="muted">
        No actions
      </span>
    );
  }

  return (
    <div>
      {/* Page Header */}

      <div className="page-header">
        <div>
          <h1>Orders</h1>

          <p>
            Create, monitor and manage
            customer orders.
          </p>
        </div>

        <motion.button
          type="button"
          className="primary-button"
          onClick={toggleForm}
          disabled={creating}
          whileHover={{
            scale: 1.02,
          }}
          whileTap={{
            scale: 0.98,
          }}
        >
          {showForm
            ? "Close Form"
            : "New Order"}
        </motion.button>
      </div>

      {/* Messages */}

      <AnimatePresence mode="popLayout">
        {error && (
          <motion.div
            key="order-error"
            className="error-message"
            role="alert"
            initial={{
              opacity: 0,
              y: -8,
            }}
            animate={{
              opacity: 1,
              y: 0,
            }}
            exit={{
              opacity: 0,
              y: -8,
            }}
            transition={{
              duration: 0.2,
            }}
          >
            {error}
          </motion.div>
        )}

        {successMessage && (
          <motion.div
            key="order-success"
            className="success-message"
            role="status"
            aria-live="polite"
            initial={{
              opacity: 0,
              y: -8,
            }}
            animate={{
              opacity: 1,
              y: 0,
            }}
            exit={{
              opacity: 0,
              y: -8,
            }}
            transition={{
              duration: 0.25,
            }}
          >
            ✓ {successMessage}
          </motion.div>
        )}
      </AnimatePresence>

      {/* Create Order Form */}

      <AnimatePresence>
        {showForm && (
          <motion.form
            key="order-form"
            className="panel order-form"
            onSubmit={handleCreate}
            initial={{
              opacity: 0,
              y: -12,
            }}
            animate={{
              opacity: 1,
              y: 0,
            }}
            exit={{
              opacity: 0,
              y: -12,
            }}
            transition={{
              duration: 0.25,
              ease: "easeOut",
            }}
          >
            <div className="form-field">
              <label htmlFor="order-customer">
                Customer
              </label>

              <select
                id="order-customer"
                value={customerId}
                onChange={(event) =>
                  setCustomerId(
                    event.target.value
                  )
                }
                required
                disabled={creating}
              >
                {customers.length ===
                  0 && (
                  <option value="">
                    No customers available
                  </option>
                )}

                {customers.map(
                  (customer) => (
                    <option
                      key={customer.id}
                      value={customer.id}
                    >
                      {customer.name} —{" "}
                      {customer.email}
                    </option>
                  )
                )}
              </select>
            </div>

            <div className="form-field">
              <label htmlFor="order-country">
                Country
              </label>

              <select
                id="order-country"
                value={countryCode}
                onChange={(event) => {
                  const newCountry =
                    event.target.value;

                  setCountryCode(
                    newCountry
                  );

                  const currencies =
                    CURRENCY_BY_COUNTRY[
                      newCountry
                    ];

                  if (
                    currencies &&
                    currencies.length >
                      0
                  ) {
                    setCurrencyCode(
                      currencies[0]
                    );
                  }
                }}
                required
                disabled={creating}
              >
                <option value="AO">
                  Angola (AO)
                </option>

                <option value="BW">
                  Botswana (BW)
                </option>

                <option value="KM">
                  Comoros (KM)
                </option>

                <option value="CD">
                  Democratic Republic of
                  the Congo (CD)
                </option>

                <option value="SZ">
                  Eswatini (SZ)
                </option>

                <option value="LS">
                  Lesotho (LS)
                </option>

                <option value="MG">
                  Madagascar (MG)
                </option>

                <option value="MW">
                  Malawi (MW)
                </option>

                <option value="MU">
                  Mauritius (MU)
                </option>

                <option value="MZ">
                  Mozambique (MZ)
                </option>

                <option value="NA">
                  Namibia (NA)
                </option>

                <option value="SC">
                  Seychelles (SC)
                </option>

                <option value="ZA">
                  South Africa (ZA)
                </option>

                <option value="TZ">
                  Tanzania (TZ)
                </option>

                <option value="ZM">
                  Zambia (ZM)
                </option>

                <option value="ZW">
                  Zimbabwe (ZW)
                </option>
              </select>
            </div>

            <div className="form-field">
              <label htmlFor="order-currency">
                Currency
              </label>

              <select
                id="order-currency"
                value={currencyCode}
                onChange={(event) =>
                  setCurrencyCode(
                    event.target.value
                  )
                }
                required
                disabled={creating}
              >
                {availableCurrencies.map(
                  (currency) => (
                    <option
                      key={currency}
                      value={currency}
                    >
                      {currency}
                    </option>
                  )
                )}
              </select>
            </div>

            <div className="order-details-section">
              <div className="modal-header">
                <div>
                  <h3>Line Items</h3>
                  <p className="muted">
                    Add one or more products to this order.
                  </p>
                </div>

                <motion.button
                  type="button"
                  className="secondary-button"
                  onClick={addLineItem}
                  disabled={creating}
                  whileTap={{ scale: 0.97 }}
                >
                  + Add Line Item
                </motion.button>
              </div>

              {lineItems.map((item, index) => (
                <div
                  className="panel"
                  key={`line-item-${index}`}
                >
                  <div className="modal-header">
                    <strong>Line Item {index + 1}</strong>

                    {lineItems.length > 1 && (
                      <button
                        type="button"
                        className="secondary-button"
                        onClick={() => removeLineItem(index)}
                        disabled={creating}
                      >
                        Remove
                      </button>
                    )}
                  </div>

                  <div className="form-field">
                    <label htmlFor={`product-code-${index}`}>
                      Product Code
                    </label>
                    <input
                      id={`product-code-${index}`}
                      type="text"
                      value={item.productCode}
                      onChange={(event) =>
                        updateLineItem(
                          index,
                          "productCode",
                          event.target.value
                        )
                      }
                      placeholder="e.g. AMR-001"
                      required
                      disabled={creating}
                    />
                  </div>

                  <div className="form-field">
                    <label htmlFor={`product-description-${index}`}>
                      Description
                    </label>
                    <input
                      id={`product-description-${index}`}
                      type="text"
                      value={item.description}
                      onChange={(event) =>
                        updateLineItem(
                          index,
                          "description",
                          event.target.value
                        )
                      }
                      placeholder="Product description"
                      required
                      disabled={creating}
                    />
                  </div>

                  <div className="form-field">
                    <label htmlFor={`quantity-${index}`}>
                      Quantity
                    </label>
                    <input
                      id={`quantity-${index}`}
                      type="number"
                      min="1"
                      step="1"
                      value={item.quantity}
                      onChange={(event) =>
                        updateLineItem(
                          index,
                          "quantity",
                          Math.max(1, Number(event.target.value))
                        )
                      }
                      required
                      disabled={creating}
                    />
                  </div>

                  <div className="form-field">
                    <label htmlFor={`unit-price-${index}`}>
                      Unit Price
                    </label>
                    <input
                      id={`unit-price-${index}`}
                      type="number"
                      min="0"
                      step="0.01"
                      value={item.unitPrice}
                      onChange={(event) =>
                        updateLineItem(
                          index,
                          "unitPrice",
                          Math.max(0, Number(event.target.value))
                        )
                      }
                      required
                      disabled={creating}
                    />
                  </div>

                  <div className="form-field">
                    <label>Line Total</label>
                    <div className="total-preview">
                      {currencyCode}{" "}
                      {(item.quantity * item.unitPrice).toFixed(2)}
                    </div>
                  </div>
                </div>
              ))}
            </div>

            <div className="form-field">
              <label>
                Estimated Total
              </label>

              <div className="total-preview">
                {currencyCode}{" "}
                {estimatedTotal.toFixed(
                  2
                )}
              </div>
            </div>

            <div className="form-actions">
              <motion.button
                className="primary-button"
                type="submit"
                disabled={
                  creating ||
                  !customerId ||
                  hasInvalidLineItems
                }
                whileTap={
                  creating
                    ? undefined
                    : {
                        scale: 0.98,
                      }
                }
              >
                {creating ? (
                  <span className="button-loading-content">
                    <motion.span
                      className="button-spinner"
                      aria-hidden="true"
                      animate={{
                        rotate: 360,
                      }}
                      transition={{
                        duration: 0.75,
                        repeat: Infinity,
                        ease: "linear",
                      }}
                    />

                    Creating order...
                  </span>
                ) : (
                  "Create Order"
                )}
              </motion.button>
            </div>
          </motion.form>
        )}
      </AnimatePresence>

      {/* Filters and Sorting */}

      <div className="toolbar">
        <select
          aria-label="Filter orders by status"
          value={statusFilter}
          onChange={(event) => {
            void handleFilterChange(
              event.target.value
            );
          }}
          disabled={loading}
        >
          <option value="">
            All statuses
          </option>

          <option value="Pending">
            Pending
          </option>

          <option value="Paid">
            Paid
          </option>

          <option value="Fulfilled">
            Fulfilled
          </option>

          <option value="Cancelled">
            Cancelled
          </option>
        </select>

        <select
          aria-label="Sort orders by"
          value={sortBy}
          onChange={(event) => {
            void handleSortByChange(
              event.target.value as OrderSortBy
            );
          }}
          disabled={loading}
        >
          <option value="createdAt">
            Created Date
          </option>

          <option value="totalAmount">
            Total Amount
          </option>
        </select>

        <select
          aria-label="Sort direction"
          value={descending ? "desc" : "asc"}
          onChange={(event) => {
            void handleSortDirectionChange(
              event.target.value === "desc"
            );
          }}
          disabled={loading}
        >
          {sortBy === "createdAt" ? (
            <>
              <option value="desc">
                Newest First
              </option>

              <option value="asc">
                Oldest First
              </option>
            </>
          ) : (
            <>
              <option value="desc">
                Highest First
              </option>

              <option value="asc">
                Lowest First
              </option>
            </>
          )}
        </select>
      </div>

      {/* Orders Table */}

      <div className="panel">
        {loading ? (
          <motion.div
            className="loading-state"
            role="status"
            aria-live="polite"
            initial={{
              opacity: 0,
            }}
            animate={{
              opacity: 1,
            }}
          >
            <motion.span
              className="loading-spinner"
              aria-hidden="true"
              animate={{
                rotate: 360,
              }}
              transition={{
                duration: 0.75,
                repeat: Infinity,
                ease: "linear",
              }}
            />

            Loading orders...
          </motion.div>
        ) : orders.length === 0 ? (
          <p className="muted">
            No orders found.
          </p>
        ) : (
          <motion.div
            className="table-container"
            initial={{
              opacity: 0,
            }}
            animate={{
              opacity: 1,
            }}
            transition={{
              duration: 0.2,
            }}
          >
            <table>
              <thead>
                <tr>
                  <th>Order</th>
                  <th>Customer</th>
                  <th>Country</th>
                  <th>Total</th>
                  <th>Status</th>
                  <th>Created</th>
                  <th>Actions</th>
                </tr>
              </thead>

              <tbody>
                {orders.map(
                  (
                    order,
                    index
                  ) => {
                    const customer =
                      customers.find(
                        (item) =>
                          item.id ===
                          order.customerId
                      );

                    return (
                      <motion.tr
                        key={order.id}
                        initial={{
                          opacity: 0,
                          y: 5,
                        }}
                        animate={{
                          opacity: 1,
                          y: 0,
                        }}
                        transition={{
                          duration: 0.18,
                          delay:
                            Math.min(
                              index *
                                0.025,
                              0.2
                            ),
                        }}
                      >
                        <td>
                          <span className="order-id">
                            {order.id.slice(
                              0,
                              8
                            )}
                            ...
                          </span>
                        </td>

                        <td>
                          {customer
                            ? customer.name
                            : order.customerId.slice(
                                0,
                                8
                              )}
                        </td>

                        <td>
                          {
                            order.countryCode
                          }
                        </td>

                        <td>
                          <strong>
                            {
                              order.currencyCode
                            }{" "}
                            {order.totalAmount.toFixed(
                              2
                            )}
                          </strong>
                        </td>

                        <td>
                          <span
                            className={`status-badge status-${order.status.toLowerCase()}`}
                          >
                            {order.status}
                          </span>
                        </td>

                        <td>
                          {new Date(
                            order.createdAt
                          ).toLocaleDateString()}
                        </td>

                        <td>
                          <div className="action-buttons">
                            <motion.button
                              type="button"
                              className="small-button"
                              onClick={() => {
                                void openOrderDetails(
                                  order.id
                                );
                              }}
                              disabled={
                                loadingOrderDetails
                              }
                              whileTap={{
                                scale: 0.96,
                              }}
                            >
                              View Details
                            </motion.button>

                            {renderOrderActions(
                              order
                            )}
                          </div>
                        </td>
                      </motion.tr>
                    );
                  }
                )}
              </tbody>
            </table>
          </motion.div>
        )}

        {/* Pagination */}

        <div className="pagination">
          <span>
            Page{" "}
            <strong>{page}</strong>
            {" "}of{" "}
            <strong>
              {totalPages}
            </strong>
            {" · "}
            {totalCount} order
            {totalCount === 1
              ? ""
              : "s"}
          </span>

          <div>
            <motion.button
              type="button"
              className="secondary-button"
              onClick={() => {
                void goToPreviousPage();
              }}
              disabled={
                page <= 1 ||
                loading
              }
              whileTap={{
                scale: 0.97,
              }}
            >
              Previous
            </motion.button>

            <motion.button
              type="button"
              className="secondary-button"
              onClick={() => {
                void goToNextPage();
              }}
              disabled={
                page >=
                  totalPages ||
                loading
              }
              whileTap={{
                scale: 0.97,
              }}
            >
              Next
            </motion.button>
          </div>
        </div>
      </div>

      {/* Order Details Modal */}

      <AnimatePresence>
        {(selectedOrder ||
          loadingOrderDetails ||
          orderDetailsError) && (
          <motion.div
            className="modal-backdrop"
            initial={{
              opacity: 0,
            }}
            animate={{
              opacity: 1,
            }}
            exit={{
              opacity: 0,
            }}
            onClick={
              closeOrderDetails
            }
          >
            <motion.div
              className="order-details-modal"
              role="dialog"
              aria-modal="true"
              aria-labelledby="order-details-title"
              initial={{
                opacity: 0,
                scale: 0.96,
                y: 15,
              }}
              animate={{
                opacity: 1,
                scale: 1,
                y: 0,
              }}
              exit={{
                opacity: 0,
                scale: 0.96,
                y: 15,
              }}
              transition={{
                duration: 0.2,
              }}
              onClick={(event) =>
                event.stopPropagation()
              }
            >
              {loadingOrderDetails ? (
                <div
                  className="loading-state"
                  role="status"
                  aria-live="polite"
                >
                  <motion.span
                    className="loading-spinner"
                    aria-hidden="true"
                    animate={{
                      rotate: 360,
                    }}
                    transition={{
                      duration: 0.75,
                      repeat: Infinity,
                      ease: "linear",
                    }}
                  />

                  Loading order details...
                </div>
              ) : orderDetailsError ? (
                <>
                  <div className="modal-header">
                    <h2 id="order-details-title">
                      Order Details
                    </h2>

                    <button
                      type="button"
                      className="secondary-button"
                      onClick={
                        closeOrderDetails
                      }
                    >
                      Close
                    </button>
                  </div>

                  <div
                    className="error-message"
                    role="alert"
                  >
                    {orderDetailsError}
                  </div>
                </>
              ) : selectedOrder ? (
                <>
                  <div className="modal-header">
                    <div>
                      <h2 id="order-details-title">
                        Order Details
                      </h2>

                      <p className="muted">
                        {selectedOrder.id}
                      </p>
                    </div>

                    <button
                      type="button"
                      className="secondary-button"
                      onClick={
                        closeOrderDetails
                      }
                      disabled={
                        updatingOrderId !==
                        null
                      }
                    >
                      Close
                    </button>
                  </div>

                  {/* Order Summary */}

                  <div className="order-details-grid">
                    <div>
                      <span className="detail-label">
                        Customer
                      </span>

                      <strong>
                        {customers.find(
                          (customer) =>
                            customer.id ===
                            selectedOrder.customerId
                        )?.name ??
                          selectedOrder.customerId}
                      </strong>
                    </div>

                    <div>
                      <span className="detail-label">
                        Status
                      </span>

                      <span
                        className={`status-badge status-${selectedOrder.status.toLowerCase()}`}
                      >
                        {selectedOrder.status}
                      </span>
                    </div>

                    <div>
                      <span className="detail-label">
                        Country
                      </span>

                      <strong>
                        {
                          selectedOrder.countryCode
                        }
                      </strong>
                    </div>

                    <div>
                      <span className="detail-label">
                        Currency
                      </span>

                      <strong>
                        {
                          selectedOrder.currencyCode
                        }
                      </strong>
                    </div>

                    <div>
                      <span className="detail-label">
                        Created
                      </span>

                      <strong>
                        {new Date(
                          selectedOrder.createdAt
                        ).toLocaleString()}
                      </strong>
                    </div>

                    <div>
                      <span className="detail-label">
                        Updated
                      </span>

                      <strong>
                        {selectedOrder.updatedAt
                          ? new Date(
                              selectedOrder.updatedAt
                            ).toLocaleString()
                          : "Not updated"}
                      </strong>
                    </div>
                  </div>

                  {/* Line Items */}

                  <div className="order-details-section">
                    <h3>
                      Line Items
                    </h3>

                    {selectedOrder.lineItems
                      .length === 0 ? (
                      <p className="muted">
                        No line items found.
                      </p>
                    ) : (
                      <div className="table-container">
                        <table>
                          <thead>
                            <tr>
                              <th>
                                Product
                              </th>

                              <th>
                                Description
                              </th>

                              <th>
                                Quantity
                              </th>

                              <th>
                                Unit Price
                              </th>

                              <th>
                                Line Total
                              </th>
                            </tr>
                          </thead>

                          <tbody>
                            {selectedOrder.lineItems.map(
                              (item) => (
                                <tr
                                  key={
                                    item.id
                                  }
                                >
                                  <td>
                                    {
                                      item.productCode
                                    }
                                  </td>

                                  <td>
                                    {
                                      item.description
                                    }
                                  </td>

                                  <td>
                                    {
                                      item.quantity
                                    }
                                  </td>

                                  <td>
                                    {
                                      selectedOrder.currencyCode
                                    }{" "}
                                    {item.unitPrice.toFixed(
                                      2
                                    )}
                                  </td>

                                  <td>
                                    <strong>
                                      {
                                        selectedOrder.currencyCode
                                      }{" "}
                                      {item.lineTotal.toFixed(
                                        2
                                      )}
                                    </strong>
                                  </td>
                                </tr>
                              )
                            )}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </div>

                  {/* Total */}

                  <div className="order-details-total">
                    <span>
                      Order Total
                    </span>

                    <strong>
                      {
                        selectedOrder.currencyCode
                      }{" "}
                      {selectedOrder.totalAmount.toFixed(
                        2
                      )}
                    </strong>
                  </div>

                  {/* Status Actions */}

                  <div className="order-details-section">
                    <h3>
                      Status Actions
                    </h3>

                    {renderOrderActions(
                      selectedOrder
                    )}
                  </div>
                </>
              ) : null}
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

export default OrdersPage;