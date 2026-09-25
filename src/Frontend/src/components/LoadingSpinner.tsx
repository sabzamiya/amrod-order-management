import { motion } from "framer-motion";

interface LoadingSpinnerProps {
  message?: string;
}

export default function LoadingSpinner({
  message = "Processing...",
}: LoadingSpinnerProps) {
  return (
    <motion.div
      className="loading-state"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      role="status"
      aria-live="polite"
    >
      <motion.div
        className="loading-spinner"
        animate={{ rotate: 360 }}
        transition={{
          duration: 0.8,
          repeat: Infinity,
          ease: "linear",
        }}
      />

      <span>{message}</span>
    </motion.div>
  );
}