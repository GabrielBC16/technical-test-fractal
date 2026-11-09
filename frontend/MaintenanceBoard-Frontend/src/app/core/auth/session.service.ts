import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { tap } from 'rxjs';
import { API_BASE_URL } from '../api/api.config';
import { isUserSession, LoginRequest, UserSession } from './session.models';

const STORAGE_KEY = 'maintenance-board.session';

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);
  private readonly currentUser = signal<UserSession | null>(this.restore());
  readonly user = this.currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this.user() !== null);
  readonly canManageTickets = computed(() => this.user()?.role === 'Operator' || this.user()?.role === 'Supervisor');

  /** Store only the API identity; passwords never enter browser storage. */
  login(request: LoginRequest) {
    return this.http.post<UserSession>(`${this.apiUrl}/auth/login`, request).pipe(tap(user => {
      if (!isUserSession(user)) throw new Error('Invalid login response.');
      this.currentUser.set(user);
      try { sessionStorage.setItem(STORAGE_KEY, JSON.stringify(user)); } catch { /* Keep the in-memory session when storage is unavailable. */ }
    }));
  }

  /** Clear local identity; the API has no logout endpoint or token. */
  logout(): void {
    this.currentUser.set(null);
    try { sessionStorage.removeItem(STORAGE_KEY); } catch { /* Storage can be disabled by browser policy. */ }
  }

  /** Treat browser storage as untrusted and tolerate unavailable storage. */
  private restore(): UserSession | null {
    try {
      const value: unknown = JSON.parse(sessionStorage.getItem(STORAGE_KEY) ?? 'null');
      return isUserSession(value) ? value : null;
    } catch { return null; }
  }
}
