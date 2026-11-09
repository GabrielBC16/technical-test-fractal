import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { authenticatedGuard, guestGuard, managementGuard } from './session.guards';
import { sessionInterceptor } from './session.interceptor';
import { SessionService } from './session.service';

describe('Session boundary', () => {
  beforeEach(() => {
    sessionStorage.setItem('maintenance-board.session', JSON.stringify({
      userId: 19, userName: 'requester', names: 'Gabriel', lastNames: 'Barrantes', role: 'Requester',
    }));
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([sessionInterceptor])), provideHttpClientTesting()],
    });
  });

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    sessionStorage.clear();
  });

  it('sends the returned user ID only to protected API requests', () => {
    const http = TestBed.inject(HttpClient);
    const controller = TestBed.inject(HttpTestingController);
    for (const url of ['http://localhost:5213/api/tickets/board', 'https://example.com/data', 'http://localhost:5213/api-other', 'http://localhost:5213/api/auth/login']) {
      http.get(url).subscribe();
      const request = controller.expectOne(url);
      expect(request.request.headers.get('X-User-Id')).toBe(url.endsWith('/board') ? '19' : null);
      request.flush({});
    }
  });

  it('keeps the session on 403 and expires it on 401', () => {
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const session = TestBed.inject(SessionService);
    const http = TestBed.inject(HttpClient);
    const controller = TestBed.inject(HttpTestingController);
    http.get('http://localhost:5213/api/users').subscribe({ error: () => {} });
    controller.expectOne('http://localhost:5213/api/users').flush({}, { status: 403, statusText: 'Forbidden' });
    expect(session.isAuthenticated()).toBe(true);
    http.get('http://localhost:5213/api/tickets/board').subscribe({ error: () => {} });
    controller.expectOne('http://localhost:5213/api/tickets/board').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(session.isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { expired: '1' } });
  });

  it('redirects guests and restricts management routes by role', () => {
    const router = TestBed.inject(Router);
    const run = (guard: typeof authenticatedGuard) => TestBed.runInInjectionContext(() => guard({} as never, {} as never));
    expect(run(authenticatedGuard)).toBe(true);
    expect(router.serializeUrl(run(guestGuard) as ReturnType<Router['createUrlTree']>)).toBe('/board');
    expect(router.serializeUrl(run(managementGuard) as ReturnType<Router['createUrlTree']>)).toBe('/board');
    TestBed.inject(SessionService).logout();
    expect(router.serializeUrl(run(authenticatedGuard) as ReturnType<Router['createUrlTree']>)).toBe('/login');
  });
});
