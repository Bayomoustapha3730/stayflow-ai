import { formatRelativeTime } from "../../utils/dateTime";
import type { PropertyKnowledgeSummary } from "../../models/propertyKnowledge";

interface PropertyKnowledgeCardProps {
  item: PropertyKnowledgeSummary;
  isSelected?: boolean;
  onView: () => void;
  onEdit: () => void;
  onToggleApproval: () => void;
  onToggleActive: () => void;
  onDelete: () => void;
}

export function PropertyKnowledgeCard({
  item,
  isSelected = false,
  onView,
  onEdit,
  onToggleApproval,
  onToggleActive,
  onDelete
}: PropertyKnowledgeCardProps) {
  return (
    <article className={`sf-knowledge-card${isSelected ? " selected" : ""}`}>
      <div className="sf-knowledge-card-top">
        <div>
          <h3>{item.title}</h3>
          <p>{item.categoryLabel}</p>
        </div>
        <div className="sf-knowledge-card-badges">
          <span className={`sf-knowledge-badge ${item.canBeUsedByAI ? "eligible" : "blocked"}`}>
            {item.canBeUsedByAI ? "Approved for AI" : "Not AI eligible"}
          </span>
          <span className={`sf-knowledge-badge ${item.isApproved ? "approved" : "draft"}`}>
            {item.isApproved ? "Approved" : "Not approved"}
          </span>
          <span className={`sf-knowledge-badge ${item.isActive ? "active" : "inactive"}`}>
            {item.isActive ? "Active" : "Inactive"}
          </span>
        </div>
      </div>

      {item.summary ? <p className="sf-knowledge-summary">{item.summary}</p> : null}

      <dl className="sf-knowledge-meta">
        <div>
          <dt>Priority</dt>
          <dd>{item.priority}</dd>
        </div>
        <div>
          <dt>Tags</dt>
          <dd>{item.tags.length > 0 ? item.tags.join(", ") : "No tags"}</dd>
        </div>
        <div>
          <dt>Updated</dt>
          <dd>{formatRelativeTime(item.updatedAt)}</dd>
        </div>
      </dl>

      <div className="sf-knowledge-card-actions">
        <button type="button" onClick={onView}>View</button>
        <button type="button" onClick={onEdit}>Edit</button>
        <button type="button" onClick={onToggleApproval}>
          {item.isApproved ? "Unapprove" : "Approve"}
        </button>
        <button type="button" onClick={onToggleActive}>
          {item.isActive ? "Deactivate" : "Activate"}
        </button>
        <button type="button" className="danger" onClick={onDelete}>Delete</button>
      </div>
    </article>
  );
}
