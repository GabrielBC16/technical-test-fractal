import { API_BASE_URL } from '../../core/api/api.config';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CreateTicket } from './create-ticket';
import { TicketDetail } from './ticket-detail';

describe('Ticket panels', () => {
  beforeAll(() => {
    // JSDOM has no native dialog API; browser checks cover actual modal behavior.
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {
      configurable: true, value: function (this: HTMLDialogElement) { this.setAttribute('open', ''); },
    });
    Object.defineProperty(HTMLDialogElement.prototype, 'close', {
      configurable: true, value: function (this: HTMLDialogElement) { this.removeAttribute('open'); },
    });
  });

  afterAll(() => {
    Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal');
    Reflect.deleteProperty(HTMLDialogElement.prototype, 'close');
  });

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [{ provide: API_BASE_URL, useValue: 'http://localhost:5213/api' }, provideHttpClient(), provideHttpClientTesting()] });
  });

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    sessionStorage.clear();
  });

  it('validates required text, posts only accepted fields, and prevents duplicate submissions', () => {
    const fixture = TestBed.createComponent(CreateTicket);
    const created = vi.fn();
    fixture.componentInstance.created.subscribe(created);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const form = element.querySelector('form')!;
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectNone('http://localhost:5213/api/tickets');
    expect(element.textContent).toContain('Ingresa un título');
    const title = element.querySelector<HTMLInputElement>('#ticket-title')!;
    const description = element.querySelector<HTMLTextAreaElement>('#ticket-description')!;
    title.value = '  Equipo averiado  ';
    title.dispatchEvent(new Event('input'));
    description.value = '  No enciende.  ';
    description.dispatchEvent(new Event('input'));
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    const request = TestBed.inject(HttpTestingController).expectOne('http://localhost:5213/api/tickets');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ title: 'Equipo averiado', description: 'No enciende.' });
    request.flush({ ticketId: 55 }, { status: 201, statusText: 'Created' });
    expect(created).toHaveBeenCalledExactlyOnceWith(55);
  });

  it('retains entered text after a create error and allows the user to retry', () => {
    const fixture = TestBed.createComponent(CreateTicket);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    for (const [selector, value] of [['#ticket-title', 'Equipo averiado'], ['#ticket-description', 'No enciende.']]) {
      const field = element.querySelector<HTMLInputElement>(selector)!;
      field.value = value;
      field.dispatchEvent(new Event('input'));
    }
    element.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    TestBed.inject(HttpTestingController).expectOne('http://localhost:5213/api/tickets')
      .flush({}, { status: 400, statusText: 'Bad request' });
    fixture.detectChanges();
    expect(element.querySelector<HTMLInputElement>('#ticket-title')!.value).toBe('Equipo averiado');
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Revisa los campos');
    expect(element.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(false);
  });

  it('recovers from a missing detail and displays cause and history from the API', () => {
    const fixture = TestBed.createComponent(TicketDetail);
    fixture.componentRef.setInput('ticketId', 55);
    fixture.detectChanges();
    const controller = TestBed.inject(HttpTestingController);
    controller.expectOne('http://localhost:5213/api/tickets/55').flush({}, { status: 404, statusText: 'Not found' });
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('No se encontró');
    element.querySelector<HTMLButtonElement>('.retry-button')!.click();
    controller.expectOne('http://localhost:5213/api/tickets/55').flush({
      ticket: { ticketId: 55, title: 'Equipo reparado', description: 'Pantalla reemplazada', status: 'Closed',
        reportedByUserId: 1, reportedByDisplayName: 'Gabriel Barrantes',
        createdAtUtc: '2026-11-09T05:00:00Z', updatedAtUtc: '2026-11-09T06:00:00Z', assignees: [] },
      cause: 'Desgaste de la pantalla',
      history: [
        { historyId: 1, eventType: 'Created', actorDisplayName: 'Gabriel Barrantes', newStatus: 'Pending', createdAtUtc: '2026-11-09T05:00:00Z' },
        { historyId: 2, eventType: 'StatusChanged', actorDisplayName: 'Fernando Cabello', previousStatus: 'InProgress', newStatus: 'Closed', comment: 'Reparación completada', createdAtUtc: '2026-11-09T06:00:00Z' },
      ],
    });
    fixture.detectChanges();
    expect(element.textContent).toContain('Desgaste de la pantalla');
    expect(element.textContent).toContain('Reparación completada');
    expect(element.textContent).toContain('Este ticket está finalizado');
    expect(element.querySelector('.history li h5')?.textContent).toContain('Ticket cerrado');
  });
});
