import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { AnimatePresence, motion } from "framer-motion";

import {
  createCustomer,
  getCustomers,
} from "../api/customers";

import type { Customer } from "../types";

const SADC_COUNTRIES = [
  { code: "AO", name: "Angola" },
  { code: "BW", name: "Botswana" },
  { code: "KM", name: "Comoros" },
  { code: "CD", name: "Democratic Republic of the Congo" },
  { code: "SZ", name: "Eswatini" },
  { code: "LS", name: "Lesotho" },
  { code: "MG", name: "Madagascar" },
  { code: "MW", name: "Malawi" },
  { code: "MU", name: "Mauritius" },
  { code: "MZ", name: "Mozambique" },
  { code: "NA", name: "Namibia" },
  { code: "SC", name: "Seychelles" },
  { code: "ZA", name: "South Africa" },
  { code: "TZ", name: "Tanzania" },
  { code: "ZM", name: "Zambia" },
  { code: "ZW", name: "Zimbabwe" },
];

const PAGE_SIZE = 10;
const MINIMUM_LOADING_TIME = 600;

function CustomersPage() {
  const [customers, setCustomers] = useState<Customer[]>([]);

  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);

  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);

  const [error, setError] = useState("");
  const [successMessage, setSuccessMessage] = useState("");

  const [showForm, setShowForm] = useState(false);

  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [countryCode, setCountryCode] = useState("ZA");

  const totalPages = Math.max(
    1,
    Math.ceil(totalCount / PAGE_SIZE)
  );

  async function loadCustomers(
    requestedPage = page,
    requestedSearch = search,
    showLoading = true
  ) {
    try {
      if (showLoading) {
        setLoading(true);
      }

      setError("");

      /*
       * customers.ts signature:
       * getCustomers(search, page, pageSize)
       */
      const result = await getCustomers(
        requestedSearch,
        requestedPage,
        PAGE_SIZE
      );

      setCustomers(result.items);
      setTotalCount(result.totalCount);
    } catch (loadError) {
      setError(
        loadError instanceof Error
          ? loadError.message
          : "Unable to load customers."
      );
    } finally {
      if (showLoading) {
        setLoading(false);
      }
    }
  }

  useEffect(() => {
    void loadCustomers(1, "", true);
  }, []);

  async function handleSearch(
    event: FormEvent<HTMLFormElement>
  ) {
    event.preventDefault();

    setError("");
    setSuccessMessage("");
    setPage(1);

    await loadCustomers(
      1,
      search,
      true
    );
  }

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

    const loadingStartedAt = Date.now();

    try {
      await createCustomer({
        name: name.trim(),
        email: email.trim(),
        countryCode,
      });

      /*
       * Keep the visual loading state visible for
       * at least 600 ms when localhost responds
       * almost immediately.
       */
      const elapsed =
        Date.now() - loadingStartedAt;

      if (elapsed < MINIMUM_LOADING_TIME) {
        await new Promise<void>((resolve) => {
          window.setTimeout(
            resolve,
            MINIMUM_LOADING_TIME - elapsed
          );
        });
      }

      setName("");
      setEmail("");
      setCountryCode("ZA");

      setShowForm(false);

      setSuccessMessage(
        "Customer created successfully."
      );

      setPage(1);

      await loadCustomers(
        1,
        search,
        false
      );

      window.setTimeout(() => {
        setSuccessMessage("");
      }, 3000);
    } catch (createError) {
      setError(
        createError instanceof Error
          ? createError.message
          : "Unable to create customer."
      );
    } finally {
      setCreating(false);
    }
  }

  async function goToPreviousPage() {
    if (
      page <= 1 ||
      loading
    ) {
      return;
    }

    const previousPage = page - 1;

    setPage(previousPage);

    await loadCustomers(
      previousPage,
      search,
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

    const nextPage = page + 1;

    setPage(nextPage);

    await loadCustomers(
      nextPage,
      search,
      true
    );
  }

  function toggleForm() {
    if (creating) {
      return;
    }

    setError("");
    setSuccessMessage("");

    setShowForm(
      (current) => !current
    );
  }

  return (
    <div>
      {/* Page Header */}

      <div className="page-header">
        <div>
          <h1>Customers</h1>

          <p>
            Search, review and manage customer
            records across the SADC region.
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
            : "New Customer"}
        </motion.button>
      </div>

      {/* Messages */}

      <AnimatePresence mode="popLayout">
        {error && (
          <motion.div
            key="customer-error"
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
            key="customer-success"
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

      {/* Create Customer Form */}

      <AnimatePresence>
        {showForm && (
          <motion.form
            key="customer-form"
            className="panel form-grid"
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
              <label htmlFor="customer-name">
                Name
              </label>

              <input
                id="customer-name"
                type="text"
                value={name}
                onChange={(event) =>
                  setName(
                    event.target.value
                  )
                }
                placeholder="Customer name"
                autoComplete="name"
                required
                disabled={creating}
              />
            </div>

            <div className="form-field">
              <label htmlFor="customer-email">
                Email
              </label>

              <input
                id="customer-email"
                type="email"
                value={email}
                onChange={(event) =>
                  setEmail(
                    event.target.value
                  )
                }
                placeholder="customer@example.com"
                autoComplete="email"
                required
                disabled={creating}
              />
            </div>

            <div className="form-field">
              <label htmlFor="customer-country">
                Country
              </label>

              <select
                id="customer-country"
                value={countryCode}
                onChange={(event) =>
                  setCountryCode(
                    event.target.value
                  )
                }
                required
                disabled={creating}
              >
                {SADC_COUNTRIES.map(
                  (country) => (
                    <option
                      key={country.code}
                      value={country.code}
                    >
                      {country.name} (
                      {country.code})
                    </option>
                  )
                )}
              </select>
            </div>

            <div className="form-actions">
              <motion.button
                className="primary-button"
                type="submit"
                disabled={
                  creating ||
                  !name.trim() ||
                  !email.trim()
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

                    Creating customer...
                  </span>
                ) : (
                  "Create Customer"
                )}
              </motion.button>
            </div>
          </motion.form>
        )}
      </AnimatePresence>

      {/* Search */}

      <form
        className="toolbar"
        onSubmit={handleSearch}
      >
        <input
          type="search"
          value={search}
          onChange={(event) =>
            setSearch(
              event.target.value
            )
          }
          placeholder="Search customers by name or email..."
          aria-label="Search customers"
        />

        <button
          className="secondary-button"
          type="submit"
          disabled={loading}
        >
          Search
        </button>
      </form>

      {/* Customer Table */}

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

            Loading customers...
          </motion.div>
        ) : customers.length === 0 ? (
          <p className="muted">
            No customers found.
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
                  <th>Name</th>
                  <th>Email</th>
                  <th>Country</th>
                  <th>Created</th>
                </tr>
              </thead>

              <tbody>
                {customers.map(
                  (customer, index) => (
                    <motion.tr
                      key={customer.id}
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
                        delay: Math.min(
                          index * 0.025,
                          0.2
                        ),
                      }}
                    >
                      <td>
                        <strong>
                          {customer.name}
                        </strong>
                      </td>

                      <td>
                        {customer.email}
                      </td>

                      <td>
                        {
                          customer.countryCode
                        }
                      </td>

                      <td>
                        {new Date(
                          customer.createdAt
                        ).toLocaleDateString()}
                      </td>
                    </motion.tr>
                  )
                )}
              </tbody>
            </table>
          </motion.div>
        )}

        {/* Pagination */}

        <div className="pagination">
          <span>
            Page <strong>{page}</strong> of{" "}
            <strong>{totalPages}</strong>
            {" · "}
            {totalCount} customer
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
                page >= totalPages ||
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
    </div>
  );
}

export default CustomersPage;