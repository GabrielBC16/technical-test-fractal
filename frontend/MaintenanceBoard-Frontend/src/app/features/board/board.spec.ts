import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { sessionInterceptor } from '../../core/auth/session.interceptor';
import { TicketBoard, TicketListItem } from '../../core/tickets/ticket.models';
import { Board } from './board';

const ticket: TicketListItem = {
  ticketId: 1042, title: 'Cajero fuera de servicio', description: 'Pantalla sin respuesta.',
  status: 'Pending', reportedByUserId: 1, reportedByDisplayName: 'Gabriel Barrantes',
  createdAtUtc: '2026-11-07T14:24:00Z', updatedAtUtc: '2026-11-07T14:24:00Z',
  assignees: [{ userId: 2, names: 'Fernando', lastNames: 'Cabello', assignedByUserId: 3, justification: null, assignedAtUtc: '2026-11-07T14:24:00Z' }],
};
const board: TicketBoard = { pending: [ticket], inProgress: [], closed: [] };

describe('Desktop board', () => {
  beforeEach(() => {
    sessionStorage.setItem('maintenance-board.session', JSON.stringify({
      userId: 1, userName: 'gbarrantes', names: 'Gabriel', lastNames: 'Barrantes', role: 'Requester',
    }));
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([sessionInterceptor])), provideHttpClientTesting()],
    });
  });

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    sessionStorage.clear();
  });

  it('loads the authenticated board and renders three columns with real ticket data', () => {
    const fixture = TestBed.createComponent(Board);
    fixture.detectChanges();
    const request = TestBed.inject(HttpTestingController).expectOne('http://localhost:5213/api/tickets/board');
    expect(request.request.method).toBe('GET');
    expect(request.request.headers.get('X-User-Id')).toBe('1');
    expect(fixture.nativeElement.textContent).toContain('Cargando tickets');
    request.flush(board);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelectorAll('.kanban-column').length).toBe(3);
    expect(element.textContent).toContain('Cajero fuera de servicio');
    expect(element.textContent).toContain('Fernando Cabello');
    expect(element.textContent).toContain('Gabriel Barrantes');
    expect(element.textContent).not.toContain('Rechazado');
  });

  it('filters by ID, clears filters, and supports a board with no tickets', () => {
    const fixture = TestBed.createComponent(Board);
    const controller = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    controller.expectOne('http://localhost:5213/api/tickets/board').flush(board);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const search = element.querySelector('input')!;
    search.value = '#999';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(element.textContent).toContain('No hay coincidencias');
    const clear = [...element.querySelectorAll('button')].find(button => button.textContent === 'Limpiar filtros')!;
    clear.click();
    fixture.detectChanges();
    expect(element.querySelectorAll('app-ticket-card').length).toBe(1);
    const refresh = [...element.querySelectorAll('button')].find(button => button.textContent === 'Actualizar')!;
    refresh.click();
    controller.expectOne('http://localhost:5213/api/tickets/board').flush({ pending: [], inProgress: [], closed: [] });
    fixture.detectChanges();
    expect(element.textContent).toContain('Aún no hay tickets');
    expect(element.querySelectorAll('app-ticket-card').length).toBe(0);
  });

  it('shows a Spanish error and can retry without recreating the page', () => {
    const fixture = TestBed.createComponent(Board);
    const controller = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    controller.expectOne('http://localhost:5213/api/tickets/board').flush({}, { status: 500, statusText: 'Server error' });
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('No se pudo cargar');
    const retry = [...element.querySelectorAll('button')].find(button => button.textContent === 'Reintentar')!;
    retry.click();
    controller.expectOne('http://localhost:5213/api/tickets/board').flush(board);
    fixture.detectChanges();
    expect(element.querySelectorAll('app-ticket-card').length).toBe(1);
    expect(element.querySelector('[role="alert"]')).toBeNull();
  });
});
