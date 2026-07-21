import { ConversationStatus } from "../../models/enums";
import { formatStatusLabel } from "../../utils/dateTime";

interface HostConversationFiltersProps {
  search: string;
  status?: ConversationStatus;
  requiresHostAttention?: boolean;
  pageSize: number;
  onSearchChange: (value: string) => void;
  onStatusChange: (value?: ConversationStatus) => void;
  onRequiresHostAttentionChange: (value?: boolean) => void;
  onPageSizeChange: (value: number) => void;
}

export function HostConversationFilters({
  search,
  status,
  requiresHostAttention,
  pageSize,
  onSearchChange,
  onStatusChange,
  onRequiresHostAttentionChange,
  onPageSizeChange
}: HostConversationFiltersProps) {
  return (
    <section className="sf-host-filters" aria-label="Conversation filters">
      <label>
        Search
        <input
          type="search"
          value={search}
          onChange={(event) => onSearchChange(event.target.value)}
          placeholder="Guest, email, property, reservation"
        />
      </label>

      <label>
        Status
        <select
          value={status === undefined ? "" : String(status)}
          onChange={(event) => {
            const value = event.target.value;
            onStatusChange(value === "" ? undefined : Number(value) as ConversationStatus);
          }}
        >
          <option value="">All statuses</option>
          {Object.values(ConversationStatus)
            .filter((value): value is ConversationStatus => typeof value === "number")
            .map((value) => (
              <option key={value} value={value}>
                {formatStatusLabel(value)}
              </option>
            ))}
        </select>
      </label>

      <label className="sf-host-checkbox-row">
        <input
          type="checkbox"
          checked={Boolean(requiresHostAttention)}
          onChange={(event) => onRequiresHostAttentionChange(event.target.checked ? true : undefined)}
        />
        Requires host attention
      </label>

      <label>
        Page size
        <select value={String(pageSize)} onChange={(event) => onPageSizeChange(Number(event.target.value))}>
          <option value="10">10</option>
          <option value="25">25</option>
          <option value="50">50</option>
        </select>
      </label>
    </section>
  );
}
