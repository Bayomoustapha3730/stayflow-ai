import {
  PropertyKnowledgeCategory,
  propertyKnowledgeCategoryLabels,
  propertyKnowledgeCategoryOptions
} from "../../models/propertyKnowledge";

interface PropertyKnowledgeFiltersProps {
  search: string;
  category?: PropertyKnowledgeCategory;
  isApproved?: boolean;
  isActive?: boolean;
  pageSize: number;
  onSearchChange: (value: string) => void;
  onCategoryChange: (value?: PropertyKnowledgeCategory) => void;
  onApprovalChange: (value?: boolean) => void;
  onActiveChange: (value?: boolean) => void;
  onPageSizeChange: (value: number) => void;
}

export function PropertyKnowledgeFilters({
  search,
  category,
  isApproved,
  isActive,
  pageSize,
  onSearchChange,
  onCategoryChange,
  onApprovalChange,
  onActiveChange,
  onPageSizeChange
}: PropertyKnowledgeFiltersProps) {
  return (
    <section className="sf-knowledge-filters" aria-label="Property knowledge filters">
      <label>
        Search
        <input
          type="search"
          value={search}
          onChange={(event) => onSearchChange(event.target.value)}
          placeholder="Title, summary, content, tags"
        />
      </label>

      <label>
        Category
        <select
          value={category === undefined ? "" : String(category)}
          onChange={(event) => {
            const value = event.target.value;
            onCategoryChange(value === "" ? undefined : Number(value) as PropertyKnowledgeCategory);
          }}
        >
          <option value="">All categories</option>
          {propertyKnowledgeCategoryOptions.map((option) => (
            <option key={option.value} value={option.value}>{propertyKnowledgeCategoryLabels[option.value]}</option>
          ))}
        </select>
      </label>

      <label>
        Approval
        <select
          value={isApproved === undefined ? "" : String(isApproved)}
          onChange={(event) => {
            const value = event.target.value;
            onApprovalChange(value === "" ? undefined : value === "true");
          }}
        >
          <option value="">All approval states</option>
          <option value="true">Approved</option>
          <option value="false">Not approved</option>
        </select>
      </label>

      <label>
        Active state
        <select
          value={isActive === undefined ? "" : String(isActive)}
          onChange={(event) => {
            const value = event.target.value;
            onActiveChange(value === "" ? undefined : value === "true");
          }}
        >
          <option value="">All states</option>
          <option value="true">Active</option>
          <option value="false">Inactive</option>
        </select>
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
