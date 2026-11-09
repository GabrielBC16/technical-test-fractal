import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { SessionService } from './core/auth/session.service';
import { isUserSession, UserSession } from './core/auth/session.models';

const user: UserSession = { userId: 7, userName: 'operator', names: 'Ana', lastNames: 'Torres', role: 'Operator' };

describe('Session foundation', () => {
  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
  });

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    sessionStorage.clear();
  });

  it('creates the application', () => {
    expect(TestBed.createComponent(App).componentInstance).toBeTruthy();
  });

  it('stores the returned identity without storing the password and clears it on logout', () => {
    const session = TestBed.inject(SessionService);
    session.login({ userName: 'operator', password: ' secret ' }).subscribe();
    const request = TestBed.inject(HttpTestingController).expectOne('http://localhost:5213/api/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.password).toBe(' secret ');
    request.flush(user);
    expect(session.user()?.userId).toBe(7);
    expect(session.canManageTickets()).toBe(true);
    expect(sessionStorage.getItem('maintenance-board.session')).not.toContain('secret');
    session.logout();
    expect(session.isAuthenticated()).toBe(false);
    expect(sessionStorage.getItem('maintenance-board.session')).toBeNull();
  });

  it('recovers from malformed storage and rejects unknown roles', () => {
    sessionStorage.setItem('maintenance-board.session', '{broken');
    expect(TestBed.inject(SessionService).user()).toBeNull();
    expect(isUserSession({ ...user, role: 'Admin' })).toBe(false);
    expect(isUserSession({ ...user, userId: -1 })).toBe(false);
  });
});
