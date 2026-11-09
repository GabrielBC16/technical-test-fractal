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
