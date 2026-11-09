import { Component, input } from '@angular/core';
import { TicketListItem, TICKET_STATUS_LABELS } from '../../core/tickets/ticket.models';
import { initials } from '../../core/tickets/ticket-presentation';

@Component({
  selector: 'app-ticket-card',
  template: `
    <article class="ticket-card" [attr.aria-labelledby]="'ticket-title-' + ticket().ticketId">
      <p class="ticket-number">#{{ ticket().ticketId }}</p>
      <h3 [id]="'ticket-title-' + ticket().ticketId">{{ ticket().title }}</h3>
      <p class="ticket-description">{{ ticket().description }}</p>
      <span class="status-badge" [attr.data-status]="ticket().status"><span aria-hidden="true">●</span>{{ labels[ticket().status] }}</span>
      <div class="assignee-row">
        @if (ticket().assignees[0]; as assignee) {
          <span class="avatar" aria-hidden="true">{{ getInitials(assignee.names + ' ' + assignee.lastNames) }}</span>
          <span class="assignee-name" [title]="assigneeNames()">{{ assignee.names }} {{ assignee.lastNames }}@if (ticket().assignees.length > 1) { · +{{ ticket().assignees.length - 1 }} }</span>
        } @else {
          <span class="avatar unassigned" aria-hidden="true">—</span><span>Sin asignar</span>
        }
      </div>
      <time [attr.datetime]="ticket().createdAtUtc">{{ localDate(ticket().createdAtUtc) }}</time>
    </article>
  `,
  styleUrl: './ticket-card.css',
})
export class TicketCard {
  readonly ticket = input.required<TicketListItem>();
  protected readonly labels = TICKET_STATUS_LABELS;
  protected readonly getInitials = initials;

  /** Expose every assignee without replacing the compact card layout. */
  protected assigneeNames(): string {
    return this.ticket().assignees.map(user => `${user.names} ${user.lastNames}`).join(', ');
  }

  /** Render the UTC API timestamp in the browser's local timezone. */
  protected localDate(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return 'Fecha no disponible';
    return new Intl.DateTimeFormat('es-PE', {
      day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false,
    }).format(date);
  }
}
