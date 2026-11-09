import { API_BASE_URL } from '../../core/api/api.config';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { sessionInterceptor } from '../../core/auth/session.interceptor';
import { localDayBoundary } from '../../core/tickets/ticket-filter';
import { TicketList } from './ticket-list';

describe('Ticket list filters', () => {
  beforeEach(() => {
    sessionStorage.setItem('maintenance-board.session', JSON.stringify({
      userId: 1, userName: 'requester', names: 'Gabriel', lastNames: 'Barrantes', role: 'Requester',
    }));
    TestBed.configureTestingModule({
      providers: [{ provide: API_BASE_URL, useValue: 'http://localhost:5213/api' }, provideRouter([]), provideHttpClient(withInterceptors([sessionInterceptor])), provideHttpClientTesting()],
    });
  });
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    sessionStorage.clear();
  });

  it('loads all statuses, including rejected tickets, with the authenticated user header', () => {
    const fixture = TestBed.createComponent(TicketList);
    fixture.detectChanges();
    const request = TestBed.inject(HttpTestingController).expectOne('http://localhost:5213/api/tickets');
    expect(request.request.headers.get('X-User-Id')).toBe('1');
    expect(request.request.params.keys()).toEqual([]);
    request.flush([{
      ticketId: 4, title: 'Reporte duplicado', description: 'La atención continúa en el ticket original.',
      status: 'Rejected', reportedByDisplayName: 'Gabriel Barrantes', assignees: [],
      createdAtUtc: '2026-11-09T05:00:00Z',
    }]);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('tbody')?.textContent).toContain('Rechazado');
    expect(element.querySelector('tbody')?.textContent).toContain('Sin asignar');
    expect(element.querySelector('.ticket-link')?.getAttribute('aria-haspopup')).toBe('dialog');
  });

  it('sends exact status and UTC boundaries for an inclusive local date range', () => {
    const fixture = TestBed.createComponent(TicketList);
    fixture.detectChanges();
    const controller = TestBed.inject(HttpTestingController);
    controller.expectOne('http://localhost:5213/api/tickets').flush([]);
    const element = fixture.nativeElement as HTMLElement;
    const status = element.querySelector<HTMLSelectElement>('select')!;
    status.value = 'Rejected';
    status.dispatchEvent(new Event('change'));
    const dates = element.querySelectorAll<HTMLInputElement>('input[type="date"]');
    dates[0].value = '2026-11-09';
    dates[1].value = '2026-11-09';
    dates.forEach(input => input.dispatchEvent(new Event('input')));
    element.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    const request = controller.expectOne(request => request.params.get('status') === 'Rejected');
    expect(request.request.url).toBe('http://localhost:5213/api/tickets');
    expect(request.request.params.get('from')).toBe(new Date(2026, 10, 9).toISOString());
    expect(request.request.params.get('to')).toBe(new Date(2026, 10, 10).toISOString());
    expect(request.request.params.keys().sort()).toEqual(['from', 'status', 'to']);
    request.flush([]);
  });

  it('blocks reversed dates and supports clearing and retrying a failed list request', () => {
    const fixture = TestBed.createComponent(TicketList);
    fixture.detectChanges();
    const controller = TestBed.inject(HttpTestingController);
    controller.expectOne('http://localhost:5213/api/tickets').flush([]);
    const element = fixture.nativeElement as HTMLElement;
    const dates = element.querySelectorAll<HTMLInputElement>('input[type="date"]');
    dates[0].value = '2026-11-10';
    dates[1].value = '2026-11-09';
    dates.forEach(input => input.dispatchEvent(new Event('input')));
    element.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    fixture.detectChanges();
    controller.expectNone('http://localhost:5213/api/tickets');
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('anterior o igual');
    const clear = [...element.querySelectorAll('button')].find(button => button.textContent === 'Limpiar filtros')!;
    clear.click();
    controller.expectOne('http://localhost:5213/api/tickets').flush({}, { status: 500, statusText: 'Server error' });
    fixture.detectChanges();
    const retry = [...element.querySelectorAll('button')].find(button => button.textContent === 'Reintentar')!;
    retry.click();
    controller.expectOne('http://localhost:5213/api/tickets').flush([]);
    fixture.detectChanges();
    expect(element.textContent).toContain('No hay tickets para esta consulta');
  });

  it('validates calendar days and advances the exclusive boundary across month ends', () => {
    expect(localDayBoundary('2026-11-30', true)).toBe(new Date(2026, 11, 1).toISOString());
    expect(localDayBoundary('2028-02-29')).toBe(new Date(2028, 1, 29).toISOString());
    expect(() => localDayBoundary('2026-02-29')).toThrow();
  });
});
