import { Component, computed, DestroyRef, inject, input, OnChanges, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { TicketDetails, TicketHistoryItem, TICKET_STATUS_LABELS } from '../../core/tickets/ticket.models';
import { initials, localTicketDate } from '../../core/tickets/ticket-presentation';
import { TicketsApiService } from '../../core/tickets/tickets-api.service';
import { SidePanel } from '../../shared/side-panel/side-panel';

@Component({
  selector: 'app-ticket-detail',
  imports: [SidePanel],
  templateUrl: './ticket-detail.html',
  styleUrl: './ticket-detail.css',
})
export class TicketDetail implements OnChanges {
  readonly ticketId = input.required<number>();
  readonly dismissed = output<void>();
  protected readonly details = signal<TicketDetails | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal('');
  protected readonly labels = TICKET_STATUS_LABELS;
  protected readonly getInitials = initials;
  protected readonly localDate = localTicketDate;
  protected readonly history = computed(() => [...(this.details()?.history ?? [])].reverse());
  private readonly api = inject(TicketsApiService);
  private readonly destroyRef = inject(DestroyRef);
  private request?: Subscription;

  /** Load a fresh snapshot whenever the selected ticket changes. */
  ngOnChanges(): void {
    this.load();
  }

  /** Cancel superseded reads and retain an explicit retry path for missing or failed tickets. */
  protected load(): void {
    this.request?.unsubscribe();
    this.details.set(null);
    this.loading.set(true);
    this.error.set('');
    this.request = this.api.getDetails(this.ticketId()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: details => { this.details.set(details); this.loading.set(false); },
      error: (error: unknown) => { this.error.set(apiErrorMessage(error)); this.loading.set(false); },
    });
  }

  /** Translate history types and transition targets without changing their source data. */
  protected eventTitle(event: TicketHistoryItem): string {
    switch (event.eventType) {
      case 'Created': return 'Ticket creado';
      case 'Assigned': return 'Operador asignado';
      case 'Comment': return 'Comentario agregado';
      case 'StatusChanged':
        if (event.newStatus === 'Closed') return 'Ticket cerrado';
        if (event.newStatus === 'Rejected') return 'Ticket rechazado';
        if (event.newStatus === 'InProgress') return 'Atención iniciada';
        return 'Estado actualizado';
    }
  }
}
