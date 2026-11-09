export type TicketStatus = 'Pending' | 'InProgress' | 'Closed' | 'Rejected';

export interface TicketAssignee {
  userId: number;
  names: string;
  lastNames: string;
  assignedByUserId: number;
  justification: string | null;
  assignedAtUtc: string;
}

export interface TicketListItem {
  ticketId: number;
  title: string;
  description: string;
  status: TicketStatus;
  reportedByUserId: number;
  reportedByDisplayName: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  assignees: TicketAssignee[];
}

/** The board deliberately excludes Rejected, matching the backend read model. */
export interface TicketBoard {
  pending: TicketListItem[];
  inProgress: TicketListItem[];
  closed: TicketListItem[];
}

export const TICKET_STATUS_LABELS: Record<TicketStatus, string> = {
  Pending: 'Pendiente',
  InProgress: 'En progreso',
  Closed: 'Cerrado',
  Rejected: 'Rechazado',
};

export interface CreateTicketRequest {
  title: string;
  description: string;
}

export interface CreatedTicketResponse {
  ticketId: number;
}

export type TicketEventType = 'Created' | 'StatusChanged' | 'Assigned' | 'Comment';

export interface TicketHistoryItem {
  historyId: number;
  actorUserId: number;
  actorDisplayName: string;
  relatedUserId: number | null;
  relatedUserDisplayName: string | null;
  eventType: TicketEventType;
  previousStatus: TicketStatus | null;
  newStatus: TicketStatus | null;
  comment: string | null;
  createdAtUtc: string;
}

export interface TicketDetails {
  ticket: TicketListItem;
  cause: string | null;
  history: TicketHistoryItem[];
}
