import { Component, input, output } from '@angular/core';
import { TicketListItem, TICKET_STATUS_LABELS } from '../../core/tickets/ticket.models';
import { initials, localTicketDate } from '../../core/tickets/ticket-presentation';

@Component({
  selector: 'app-ticket-card',
  template: `
    <article class="ticket-card" [attr.aria-labelledby]="'ticket-title-' + ticket().ticketId">
      <p class="ticket-number">#{{ ticket().ticketId }}</p>
      <h3 [id]="'ticket-title-' + ticket().ticketId"><button class="text-left hover:underline" type="button" (click)="opened.emit(ticket().ticketId)" [attr.aria-label]="'Ver ticket #' + ticket().ticketId + ': ' + ticket().title">{{ ticket().title }}</button></h3>
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
  readonly opened = output<number>();
  protected readonly labels = TICKET_STATUS_LABELS;
  protected readonly getInitials = initials;
  protected readonly localDate = localTicketDate;

  /** Expose every assignee without replacing the compact card layout. */
  protected assigneeNames(): string {
    return this.ticket().assignees.map(user => `${user.names} ${user.lastNames}`).join(', ');
  }

}
