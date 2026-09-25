import { useEffect, useState } from "react";
import {
  AnimatePresence,
  motion,
} from "framer-motion";

import { authenticateForDevelopment } from "./api/auth";

import CustomersPage from "./pages/CustomersPage";
import OrdersPage from "./pages/OrdersPage";

import "./App.css";

type Page =
  | "dashboard"
  | "customers"
  | "orders";

const pageVariants = {
  initial: {
    opacity: 0,
    y: 12,
  },

  animate: {
    opacity: 1,
    y: 0,
  },

  exit: {
    opacity: 0,
    y: -8,
  },
};

const cardContainerVariants = {
  initial: {},

  animate: {
    transition: {
      staggerChildren: 0.08,
    },
  },
};

const cardVariants = {
  initial: {
    opacity: 0,
    y: 18,
  },

  animate: {
    opacity: 1,
    y: 0,
  },
};

function App() {
  const [page, setPage] =
    useState<Page>("dashboard");

  const [ready, setReady] =
    useState(false);

  const [authError, setAuthError] =
    useState("");

  useEffect(() => {
    authenticateForDevelopment()
      .then(() => {
        setReady(true);
      })
      .catch((error) => {
        setAuthError(
          error instanceof Error
            ? error.message
            : "Authentication failed."
        );
      });
  }, []);

  /*
   * Authentication error
   */
  if (authError) {
    return (
      <motion.div
        className="center-state"
        initial={{
          opacity: 0,
        }}
        animate={{
          opacity: 1,
        }}
        transition={{
          duration: 0.25,
        }}
      >
        <div>
          <h2>
            Unable to start application
          </h2>

          <p>{authError}</p>
        </div>
      </motion.div>
    );
  }

  /*
   * Initial application loading
   */
  if (!ready) {
    return (
      <div className="center-state">
        <motion.div
          className="app-loading"
          initial={{
            opacity: 0,
          }}
          animate={{
            opacity: 1,
          }}
          transition={{
            duration: 0.2,
          }}
          role="status"
          aria-live="polite"
        >
          <motion.span
            className="app-loading-spinner"
            aria-hidden="true"
            animate={{
              rotate: 360,
            }}
            transition={{
              duration: 0.8,
              repeat: Infinity,
              ease: "linear",
            }}
          />

          Loading application...
        </motion.div>
      </div>
    );
  }

  return (
    <div className="app">
      {/* Sidebar */}

      <motion.aside
        className="sidebar"
        initial={{
          opacity: 0,
          x: -20,
        }}
        animate={{
          opacity: 1,
          x: 0,
        }}
        transition={{
          duration: 0.3,
          ease: "easeOut",
        }}
      >
        {/* Brand */}

        <motion.div
          className="brand"
          initial={{
            opacity: 0,
            y: -8,
          }}
          animate={{
            opacity: 1,
            y: 0,
          }}
          transition={{
            delay: 0.1,
            duration: 0.25,
          }}
        >
          <motion.span
            className="brand-mark"
            whileHover={{
              scale: 1.05,
            }}
            transition={{
              duration: 0.15,
            }}
          >
            A
          </motion.span>

          <div>
            <strong>AMROD</strong>

            <small>
              Order Management
            </small>
          </div>
        </motion.div>

        {/* Navigation */}

        <motion.nav
          initial={{
            opacity: 0,
          }}
          animate={{
            opacity: 1,
          }}
          transition={{
            delay: 0.18,
            duration: 0.3,
          }}
          aria-label="Main navigation"
        >
          <motion.button
            type="button"
            className={
              page === "dashboard"
                ? "active"
                : ""
            }
            onClick={() =>
              setPage("dashboard")
            }
            whileTap={{
              scale: 0.98,
            }}
          >
            Dashboard
          </motion.button>

          <motion.button
            type="button"
            className={
              page === "customers"
                ? "active"
                : ""
            }
            onClick={() =>
              setPage("customers")
            }
            whileTap={{
              scale: 0.98,
            }}
          >
            Customers
          </motion.button>

          <motion.button
            type="button"
            className={
              page === "orders"
                ? "active"
                : ""
            }
            onClick={() =>
              setPage("orders")
            }
            whileTap={{
              scale: 0.98,
            }}
          >
            Orders
          </motion.button>
        </motion.nav>
      </motion.aside>

      {/* Main content */}

      <main className="content">
        <AnimatePresence mode="wait">
          {/* Dashboard */}

          {page === "dashboard" && (
            <motion.div
              key="dashboard"
              variants={pageVariants}
              initial="initial"
              animate="animate"
              exit="exit"
              transition={{
                duration: 0.25,
                ease: "easeOut",
              }}
            >
              <div className="page-header">
                <div>
                  <h1>
                    Order Management
                  </h1>

                  <p>
                    Manage customers,
                    orders and asynchronous
                    order processing across
                    the SADC region.
                  </p>
                </div>
              </div>

              <motion.div
                className="dashboard-grid"
                variants={
                  cardContainerVariants
                }
                initial="initial"
                animate="animate"
              >
                {/* Customers */}

                <motion.button
                  type="button"
                  className="dashboard-card"
                  variants={cardVariants}
                  onClick={() =>
                    setPage("customers")
                  }
                  whileHover={{
                    y: -4,
                  }}
                  whileTap={{
                    scale: 0.99,
                  }}
                  transition={{
                    duration: 0.18,
                  }}
                >
                  <span>
                    Customers
                  </span>

                  <strong>
                    Customer Management
                  </strong>

                  <p>
                    Search, paginate and
                    create customers across
                    supported SADC countries.
                  </p>
                </motion.button>

                {/* Orders */}

                <motion.button
                  type="button"
                  className="dashboard-card"
                  variants={cardVariants}
                  onClick={() =>
                    setPage("orders")
                  }
                  whileHover={{
                    y: -4,
                  }}
                  whileTap={{
                    scale: 0.99,
                  }}
                  transition={{
                    duration: 0.18,
                  }}
                >
                  <span>
                    Orders
                  </span>

                  <strong>
                    Order Management
                  </strong>

                  <p>
                    Create, filter and manage
                    customer orders and their
                    lifecycle.
                  </p>
                </motion.button>

                {/* RabbitMQ */}

                <motion.div
                  className="dashboard-card"
                  variants={cardVariants}
                  whileHover={{
                    y: -4,
                  }}
                  transition={{
                    duration: 0.18,
                  }}
                >
                  <span>
                    Messaging
                  </span>

                  <strong>
                    RabbitMQ Processing
                  </strong>

                  <p>
                    OrderCreated events are
                    processed asynchronously
                    by the background worker.
                  </p>
                </motion.div>

                {/* API */}

                <motion.div
                  className="dashboard-card"
                  variants={cardVariants}
                  whileHover={{
                    y: -4,
                  }}
                  transition={{
                    duration: 0.18,
                  }}
                >
                  <span>
                    Platform
                  </span>

                  <strong>
                    ASP.NET Core API
                  </strong>

                  <p>
                    Secured REST APIs,
                    validation, SQL Server
                    persistence and health
                    monitoring.
                  </p>
                </motion.div>
              </motion.div>
            </motion.div>
          )}

          {/* Customers */}

          {page === "customers" && (
            <motion.div
              key="customers"
              variants={pageVariants}
              initial="initial"
              animate="animate"
              exit="exit"
              transition={{
                duration: 0.25,
                ease: "easeOut",
              }}
            >
              <CustomersPage />
            </motion.div>
          )}

          {/* Orders */}

          {page === "orders" && (
            <motion.div
              key="orders"
              variants={pageVariants}
              initial="initial"
              animate="animate"
              exit="exit"
              transition={{
                duration: 0.25,
                ease: "easeOut",
              }}
            >
              <OrdersPage />
            </motion.div>
          )}
        </AnimatePresence>
      </main>
    </div>
  );
}

export default App;