import type { PagedResult } from "./chat";

export enum PropertyKnowledgeCategory {
  WiFi = 0,
  Parking = 1,
  CheckIn = 2,
  Checkout = 3,
  HouseRules = 4,
  Amenities = 5,
  Laundry = 6,
  Thermostat = 7,
  Trash = 8,
  Emergency = 9,
  Accessibility = 10,
  FAQ = 11,
  LocalRecommendations = 12,
  Maintenance = 13,
  Other = 14
}

export const propertyKnowledgeCategoryLabels: Record<PropertyKnowledgeCategory, string> = {
  [PropertyKnowledgeCategory.WiFi]: "Wi-Fi",
  [PropertyKnowledgeCategory.Parking]: "Parking",
  [PropertyKnowledgeCategory.CheckIn]: "Check-in",
  [PropertyKnowledgeCategory.Checkout]: "Checkout",
  [PropertyKnowledgeCategory.HouseRules]: "House rules",
  [PropertyKnowledgeCategory.Amenities]: "Amenities",
  [PropertyKnowledgeCategory.Laundry]: "Laundry",
  [PropertyKnowledgeCategory.Thermostat]: "Thermostat",
  [PropertyKnowledgeCategory.Trash]: "Trash",
  [PropertyKnowledgeCategory.Emergency]: "Emergency",
  [PropertyKnowledgeCategory.Accessibility]: "Accessibility",
  [PropertyKnowledgeCategory.FAQ]: "FAQ",
  [PropertyKnowledgeCategory.LocalRecommendations]: "Local recommendations",
  [PropertyKnowledgeCategory.Maintenance]: "Maintenance",
  [PropertyKnowledgeCategory.Other]: "Other"
};

export const propertyKnowledgeCategoryOptions = Object.values(PropertyKnowledgeCategory)
  .filter((value): value is PropertyKnowledgeCategory => typeof value === "number")
  .map((value) => ({
    value,
    label: propertyKnowledgeCategoryLabels[value]
  }));

export interface PropertyKnowledgeListQuery {
  search?: string;
  category?: PropertyKnowledgeCategory;
  isApproved?: boolean;
  isActive?: boolean;
  pageNumber: number;
  pageSize: number;
}

export interface PropertyKnowledgeSummary {
  id: string;
  propertyId: string;
  propertyName: string;
  category: PropertyKnowledgeCategory;
  categoryLabel: string;
  title: string;
  summary?: string | null;
  tags: string[];
  priority: number;
  isApproved: boolean;
  isActive: boolean;
  approvedAt?: string | null;
  approvedBy?: string | null;
  createdAt: string;
  updatedAt: string;
  canBeUsedByAI: boolean;
}

export interface PropertyKnowledgeDetail extends PropertyKnowledgeSummary {
  content: string;
  estimatedCharacterContribution: number;
}

export interface CreatePropertyKnowledgeRequest {
  category: PropertyKnowledgeCategory;
  title: string;
  summary?: string | null;
  content: string;
  tags: string[];
  priority: number;
  isActive: boolean;
}

export interface UpdatePropertyKnowledgeRequest extends CreatePropertyKnowledgeRequest {
}

export interface PropertyKnowledgePagedResult<T> extends PagedResult<T> {
}
