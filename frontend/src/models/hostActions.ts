export interface HostActionDecisionRequest {
  decisionNote?: string;
}

export interface HostActionListItem {
  actionId: string;
  actionType: string;
  status: string;
  conversationId: string;
  propertyId: string;
  reservationId?: string | null;
  createdAt: string;
  executedAt?: string | null;
}
