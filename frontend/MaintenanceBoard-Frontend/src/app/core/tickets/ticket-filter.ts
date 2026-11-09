import { TicketStatus } from './ticket.models';

export interface TicketListFilter {
  status?: TicketStatus;
  from?: string;
  to?: string;
}

/** Convert a local calendar day to UTC; the upper boundary is the next local midnight. */
export function localDayBoundary(value: string, exclusiveEnd = false): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) throw new Error('Invalid calendar date.');
  const [, year, month, day] = match.map(Number);
  const date = new Date(0);
  date.setHours(0, 0, 0, 0);
  date.setFullYear(year, month - 1, day);
  if (year < 1 || date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) {
    throw new Error('Invalid calendar date.');
  }
  if (exclusiveEnd) date.setDate(date.getDate() + 1);
  return date.toISOString();
}
