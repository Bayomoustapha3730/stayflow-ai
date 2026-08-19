interface PaymentStatusBadgeProps {
  status?: string | null;
}

function normalizeStatus(status?: string | null) {
  return status?.trim().toLowerCase() ?? "";
}

export function PaymentStatusBadge({
  status
}: PaymentStatusBadgeProps) {
  const normalized = normalizeStatus(status);

  const label = (() => {
    switch (normalized) {
      case "pending":
        return "Pending";
      case "processing":
        return "Processing";
      case "paid":
        return "Paid";
      case "failed":
        return "Failed";
      case "cancelled":
        return "Cancelled";
      case "expired":
        return "Expired";
      default:
        return status?.trim() || "Unknown";
    }
  })();

  return (
    <span
      className={`payment-status-badge payment-status-${normalized || "unknown"}`}
      data-payment-status={normalized || "unknown"}
    >
      {label}
    </span>
  );
}
