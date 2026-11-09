import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { catchError, map, of, startWith, Subject, switchMap } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast.service';
import { localDayBoundary, TicketListFilter } from '../../core/tickets/ticket-filter';
import { TicketListItem, TicketStatus, TICKET_STATUS_LABELS } from '../../core/tickets/ticket.models';
import { localTicketDate } from '../../core/tickets/ticket-presentation';
import { TicketsApiService } from '../../core/tickets/tickets-api.service';
import { DesktopShell } from '../../layout/desktop-shell';
import { CreateTicket } from '../tickets/create-ticket';
import { TicketDetail } from '../tickets/ticket-detail';

type ListState = { status: 'loading' | 'ready' | 'error'; items: TicketListItem[]; error: string };

@Component({
  selector: 'app-ticket-list',
  imports: [DesktopShell, ReactiveFormsModule, CreateTicket, TicketDetail],
  templateUrl: './ticket-list.html',
  styleUrl: './ticket-list.css',
})
export class TicketList {
  private readonly api = inject(TicketsApiService);
  private readonly toast = inject(ToastService);
  private readonly reload = new Subject<TicketListFilter>();
  private applied: TicketListFilter = {};
  protected readonly state = signal<ListState>({ status: 'loading', items: [], error: '' });
  protected readonly filterError = signal('');
  protected readonly search = signal('');
  protected readonly panel = signal<{ kind: 'create' } | { kind: 'detail'; ticketId: number } | null>(null);
  protected readonly labels = TICKET_STATUS_LABELS;
  protected readonly statuses = Object.keys(TICKET_STATUS_LABELS) as TicketStatus[];
  protected readonly localDate = localTicketDate;
  protected readonly form = inject(FormBuilder).nonNullable.group({
    status: ['' as TicketStatus | ''], from: [''], to: [''],
  });
  protected readonly visible = computed(() => {
    const query = this.search().trim().toLocaleLowerCase('es').replace(/^#/, '');
    return this.state().items.filter(ticket => !query
      || ticket.title.toLocaleLowerCase('es').includes(query) || String(ticket.ticketId).includes(query));
  });

  constructor() {
    // Cancel stale filter requests and keep the stream alive after a failed query.
    this.reload.pipe(
      startWith({} as TicketListFilter),
      switchMap(filter => this.api.getList(filter).pipe(
        map(items => ({ status: 'ready', items, error: '' } as ListState)),
        catchError((error: unknown) => of<ListState>({ status: 'error', items: [], error: apiErrorMessage(error) })),
        startWith<ListState>({ status: 'loading', items: [], error: '' }),
      )),
      takeUntilDestroyed(),
    ).subscribe(state => this.state.set(state));
  }

  /** Apply inclusive calendar dates as the API's inclusive-from/exclusive-to UTC boundaries. */
  protected apply(): void {
    const value = this.form.getRawValue();
    this.filterError.set('');
    if (value.from && value.to && value.from > value.to) {
      this.filterError.set('La fecha inicial debe ser anterior o igual a la fecha final.');
      return;
    }
    try {
      this.applied = {
        ...(value.status ? { status: value.status } : {}),
        ...(value.from ? { from: localDayBoundary(value.from) } : {}),
        ...(value.to ? { to: localDayBoundary(value.to, true) } : {}),
      };
    } catch {
      this.filterError.set('Ingresa fechas válidas para el rango de consulta.');
      return;
    }
    this.refresh();
  }

  /** Retry or reload the last applied query, rather than unsaved form edits. */
  protected refresh(): void {
    this.reload.next(this.applied);
  }

  /** Restore the complete list, including rejected tickets. */
  protected clearFilters(): void {
    this.form.reset();
    this.search.set('');
    this.filterError.set('');
    this.applied = {};
    this.refresh();
  }

  /** Open the returned ticket and reload an unfiltered list after successful creation. */
  protected ticketCreated(ticketId: number): void {
    this.toast.show(`Ticket #${ticketId} creado correctamente.`);
    this.clearFilters();
    this.panel.set({ kind: 'detail', ticketId });
  }

  /** Show all assignees without requesting the privileged users endpoint. */
  protected assigneeNames(ticket: TicketListItem): string {
    return ticket.assignees.map(user => `${user.names} ${user.lastNames}`).join(', ') || 'Sin asignar';
  }
}
