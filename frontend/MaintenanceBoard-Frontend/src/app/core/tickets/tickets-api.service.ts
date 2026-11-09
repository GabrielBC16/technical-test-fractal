import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../api/api.config';
import { AssignableUser, AssignTicketRequest, CreatedTicketResponse, CreateTicketRequest, TicketBoard, TicketDetails, TicketListItem, TransitionTicketRequest } from './ticket.models';
import { TicketListFilter } from './ticket-filter';

@Injectable({ providedIn: 'root' })
export class TicketsApiService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  /** Fetch the authoritative groups without sending actor IDs in the body. */
  getBoard(): Observable<TicketBoard> {
    return this.http.get<TicketBoard>(`${this.apiUrl}/tickets/board`);
  }

  /** Query all statuses with only the optional filters supported by the backend. */
  getList(filter: TicketListFilter = {}): Observable<TicketListItem[]> {
    let params = new HttpParams();
    if (filter.status) params = params.set('status', filter.status);
    if (filter.from) params = params.set('from', filter.from);
    if (filter.to) params = params.set('to', filter.to);
    return this.http.get<TicketListItem[]>(`${this.apiUrl}/tickets`, { params });
  }

  /** Send only the fields accepted by CreateTicketBody; identity comes from the interceptor. */
  create(request: CreateTicketRequest): Observable<CreatedTicketResponse> {
    return this.http.post<CreatedTicketResponse>(`${this.apiUrl}/tickets`, request);
  }

  /** Fetch the ticket, cause, assignments, and complete history together. */
  getDetails(ticketId: number): Observable<TicketDetails> {
    return this.http.get<TicketDetails>(`${this.apiUrl}/tickets/${ticketId}`);
  }

  /** Only managers may request active operators and supervisors for assignment. */
  getAssignableUsers(): Observable<AssignableUser[]> {
    return this.http.get<AssignableUser[]>(`${this.apiUrl}/users`);
  }

  /** Append one assignee and its own justification; existing assignments are immutable. */
  assign(ticketId: number, request: AssignTicketRequest): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/tickets/${ticketId}/assignments`, request);
  }

  /** Include the known status so the server can reject stale transitions atomically. */
  transition(ticketId: number, request: TransitionTicketRequest): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/tickets/${ticketId}/transitions`, request);
  }

  /** The server determines the comment author from the authenticated identity. */
  addComment(ticketId: number, comment: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/tickets/${ticketId}/comments`, { comment });
  }
}
