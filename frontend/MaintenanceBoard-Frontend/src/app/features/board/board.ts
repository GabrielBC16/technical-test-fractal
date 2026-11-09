import { Component, computed, inject, signal } from '@angular/core';
import { DesktopShell } from '../../layout/desktop-shell';
import { TicketListItem } from '../../core/tickets/ticket.models';
import { BoardStore } from './board.store';
import { TicketCard } from './ticket-card';
import { CreateTicket } from '../tickets/create-ticket';
import { TicketDetail } from '../tickets/ticket-detail';

const COLUMNS = [
  { key: 'pending', status: 'Pending', label: 'Pendiente', summary: 'Pendientes' },
  { key: 'inProgress', status: 'InProgress', label: 'En progreso', summary: 'En progreso' },
  { key: 'closed', status: 'Closed', label: 'Cerrado', summary: 'Cerrados' },
] as const;

@Component({
  selector: 'app-board',
  imports: [DesktopShell, TicketCard, CreateTicket, TicketDetail],
  providers: [BoardStore],
  templateUrl: './board.html',
  styleUrl: './board.css',
})
export class Board {
  protected readonly store = inject(BoardStore);
  protected readonly columns = COLUMNS;
  protected readonly search = signal('');
  protected readonly status = signal('');
  protected readonly assignee = signal('');
  protected readonly panel = signal<{ kind: 'create' } | { kind: 'detail'; ticketId: number } | null>(null);
  protected readonly notice = signal('');

  protected readonly assignees = computed(() => {
    const board = this.store.board();
    const users = new Map<number, string>();
    if (board) {
      for (const ticket of [...board.pending, ...board.inProgress, ...board.closed]) {
        for (const user of ticket.assignees) users.set(user.userId, `${user.names} ${user.lastNames}`);
      }
    }
    return [...users].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name, 'es'));
  });

  protected readonly visibleColumns = computed(() => {
    const board = this.store.board();
    const query = this.search().trim().toLocaleLowerCase('es').replace(/^#/, '');
    return COLUMNS.filter(column => !this.status() || column.status === this.status()).map(column => ({
      ...column,
      tickets: (board?.[column.key] ?? []).filter(ticket => this.matches(ticket, query)),
    }));
  });
  protected readonly visibleCount = computed(() => this.visibleColumns().reduce((total, column) => total + column.tickets.length, 0));
  protected readonly hasFilters = computed(() => !!(this.search() || this.status() || this.assignee()));

  /** Filter the loaded board without requesting the privileged users endpoint. */
  private matches(ticket: TicketListItem, query: string): boolean {
    return (!query || ticket.title.toLocaleLowerCase('es').includes(query) || String(ticket.ticketId).includes(query))
      && (!this.assignee() || ticket.assignees.some(user => String(user.userId) === this.assignee()));
  }

  /** Restore the complete board without losing the current server snapshot. */
  protected clearFilters(): void {
    this.search.set('');
    this.status.set('');
    this.assignee.set('');
  }

  /** Display the new server ticket and refresh the authoritative board after creation. */
  protected ticketCreated(ticketId: number): void {
    this.notice.set(`Ticket #${ticketId} creado correctamente.`);
    this.clearFilters();
    this.store.refresh();
    this.panel.set({ kind: 'detail', ticketId });
  }

}
