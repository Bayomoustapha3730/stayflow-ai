import { formatRelativeTime } from "../../utils/dateTime";
import type { PropertyKnowledgeDetail } from "../../models/propertyKnowledge";

interface PropertyKnowledgePreviewProps {
  item: PropertyKnowledgeDetail;
}

export function PropertyKnowledgePreview({ item }: PropertyKnowledgePreviewProps) {
  return (
    <section className="sf-knowledge-preview" aria-label="AI-visible preview">
      <div className="sf-knowledge-preview-header">
        <div>
          <span className={`sf-knowledge-badge ${item.canBeUsedByAI ? "eligible" : "blocked"}`}>
            {item.canBeUsedByAI ? "Approved for AI" : "Not AI eligible"}
          </span>
          <h3>{item.title}</h3>
          <p>{item.categoryLabel}</p>
        </div>
        <div className="sf-knowledge-preview-stats">
          <span>{item.estimatedCharacterContribution} characters</span>
          <span>{formatRelativeTime(item.updatedAt)}</span>
        </div>
      </div>

      <dl className="sf-knowledge-preview-grid">
        <div>
          <dt>Summary</dt>
          <dd>{item.summary || "No summary"}</dd>
        </div>
        <div>
          <dt>Content</dt>
          <dd className="sf-knowledge-preview-content">{item.content}</dd>
        </div>
        <div>
          <dt>Tags</dt>
          <dd>{item.tags.length > 0 ? item.tags.join(", ") : "No tags"}</dd>
        </div>
        <div>
          <dt>Approval</dt>
          <dd>{item.isApproved ? "Approved" : "Not approved"}</dd>
        </div>
        <div>
          <dt>Active state</dt>
          <dd>{item.isActive ? "Active" : "Inactive"}</dd>
        </div>
        <div>
          <dt>Eligibility</dt>
          <dd>{item.canBeUsedByAI ? "Can enter AI context" : "Blocked from AI context"}</dd>
        </div>
      </dl>
    </section>
  );
}
