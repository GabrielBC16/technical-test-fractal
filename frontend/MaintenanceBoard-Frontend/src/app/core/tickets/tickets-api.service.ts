import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../api/api.config';
import { TicketBoard } from './ticket.models';

@Injectable({ providedIn: 'root' })
export class TicketsApiService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  /** Fetch the authoritative groups without sending actor IDs in the body. */
  getBoard(): Observable<TicketBoard> {
    return this.http.get<TicketBoard>(`${this.apiUrl}/tickets/board`);
  }
}
