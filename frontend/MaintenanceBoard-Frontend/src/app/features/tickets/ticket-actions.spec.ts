import { API_BASE_URL } from '../../core/api/api.config';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UserRole } from '../../core/auth/session.models';
import { TicketListItem } from '../../core/tickets/ticket.models';
import { TicketActions } from './ticket-actions';

const assignee = { userId: 2, names: 'Fernando', lastNames: 'Cabello', assignedByUserId: 3, justification: null, assignedAtUtc: '2026-11-09T05:00:00Z' };
const ticket: TicketListItem = {
  ticketId: 55, title: 'Equipo averiado', description: 'No enciende', status: 'Pending',
  reportedByUserId: 1, reportedByDisplayName: 'Gabriel Barrantes',
  createdAtUtc: '2026-11-09T05:00:00Z', updatedAtUtc: '2026-11-09T05:00:00Z', assignees: [assignee],
};
const users = [
  { userId: 2, names: 'Fernando', lastNames: 'Cabello', role: 'Operator', isActive: true },
  { userId: 3, names: 'Mario', lastNames: 'Huarcaya', role: 'Supervisor', isActive: true },
];

/** Render one authenticated action surface; API requests remain isolated from real data. */
function render(role: UserRole = 'Operator', data = ticket): ComponentFixture<TicketActions> {
  sessionStorage.setItem('maintenance-board.session', JSON.stringify({
    userId: 1, userName: 'demo', names: 'Demo', lastNames: 'User', role,
  }));
  const fixture = TestBed.createComponent(TicketActions);
  fixture.componentRef.setInput('ticket', data);
  fixture.detectChanges();
  return fixture;
}

/** Trigger the same reactive-form event used by a native submit button. */
function submit(form: HTMLFormElement): void {
  form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
}

describe('Ticket action rules', () => {
  beforeAll(() => {
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: () => {} });
    Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: () => {} });
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

  it('requires a separate justification for each additional operator and omits existing users', () => {
    const fixture = render();
    const element = fixture.nativeElement as HTMLElement;
    element.querySelector<HTMLButtonElement>('.add-operator')!.click();
    TestBed.inject(HttpTestingController).expectOne('http://localhost:5213/api/users').flush(users);
    fixture.detectChanges();
    const select = element.querySelector<HTMLSelectElement>('#assigned-user')!;
    expect(select.textContent).not.toContain('Fernando');
    select.value = '3';
    select.dispatchEvent(new Event('change'));
    submit(select.closest('form')!);
    TestBed.inject(HttpTestingController).expectNone('http://localhost:5213/api/tickets/55/assignments');
    const reason = element.querySelector<HTMLTextAreaElement>('#assignment-reason')!;
    reason.value = '  Se requiere apoyo especializado  ';
    reason.dispatchEvent(new Event('input'));
    submit(select.closest('form')!);
    submit(select.closest('form')!);
    const request = TestBed.inject(HttpTestingController).expectOne('http://localhost:5213/api/tickets/55/assignments');
    expect(request.request.body).toEqual({ assignedUserId: 3, justification: 'Se requiere apoyo especializado' });
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('allows the manually selected first operator without a justification', () => {
    const fixture = render('Supervisor', { ...ticket, assignees: [] });
    const element = fixture.nativeElement as HTMLElement;
    element.querySelector<HTMLButtonElement>('.add-operator')!.click();
    TestBed.inject(HttpTestingController).expectOne('http://localhost:5213/api/users').flush(users);
    fixture.detectChanges();
    const select = element.querySelector<HTMLSelectElement>('#assigned-user')!;
    select.value = '2';
    select.dispatchEvent(new Event('change'));
    submit(select.closest('form')!);
    const request = TestBed.inject(HttpTestingController).expectOne('http://localhost:5213/api/tickets/55/assignments');
    expect(request.request.body).toEqual({ assignedUserId: 2 });
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('requires cause for closing and sends the displayed expected status', () => {
    const fixture = render('Operator', { ...ticket, status: 'InProgress' });
    const element = fixture.nativeElement as HTMLElement;
    element.querySelector<HTMLButtonElement>('.primary')!.click();
    fixture.detectChanges();
    const text = element.querySelector<HTMLTextAreaElement>('#transition-text')!;
    submit(text.closest('form')!);
    TestBed.inject(HttpTestingController).expectNone('http://localhost:5213/api/tickets/55/transitions');
    text.value = '  Desgaste de la fuente  ';
    text.dispatchEvent(new Event('input'));
    submit(text.closest('form')!);
    const request = TestBed.inject(HttpTestingController).expectOne('http://localhost:5213/api/tickets/55/transitions');
    expect(request.request.body).toEqual({ expectedStatus: 'InProgress', newStatus: 'Closed', cause: 'Desgaste de la fuente' });
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('requires a rejection reason and blocks stale resubmission until refresh', () => {
    const fixture = render();
    const element = fixture.nativeElement as HTMLElement;
    const reject = [...element.querySelectorAll('button')].find(button => button.textContent === 'Rechazar')!;
    reject.click();
    fixture.detectChanges();
    const text = element.querySelector<HTMLTextAreaElement>('#transition-text')!;
    submit(text.closest('form')!);
    TestBed.inject(HttpTestingController).expectNone('http://localhost:5213/api/tickets/55/transitions');
    text.value = 'Ticket duplicado';
    text.dispatchEvent(new Event('input'));
    submit(text.closest('form')!);
    const request = TestBed.inject(HttpTestingController).expectOne('http://localhost:5213/api/tickets/55/transitions');
    expect(request.request.body).toEqual({ expectedStatus: 'Pending', newStatus: 'Rejected', comment: 'Ticket duplicado' });
    request.flush({ code: 'ticket.concurrency' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();
    submit(text.closest('form')!);
    TestBed.inject(HttpTestingController).expectNone('http://localhost:5213/api/tickets/55/transitions');
    expect(text.value).toBe('Ticket duplicado');
    const refresh = [...element.querySelectorAll('button')].find(button => button.textContent === 'Actualizar ticket')!;
    const requested = vi.fn();
    fixture.componentInstance.refreshRequested.subscribe(requested);
    refresh.click();
    expect(requested).toHaveBeenCalledOnce();
  });

  it('restricts requester comments to reported tickets and never fetches assignable users', () => {
    const fixture = render('Requester', { ...ticket, reportedByUserId: 99 });
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.add-operator')).toBeNull();
    expect(element.querySelector('.comment-form')).toBeNull();
    fixture.componentRef.setInput('ticket', { ...ticket, status: 'Closed' });
    fixture.detectChanges();
    const text = element.querySelector<HTMLTextAreaElement>('#new-comment')!;
    text.value = '  Gracias por la atención  ';
    text.dispatchEvent(new Event('input'));
    submit(text.closest('form')!);
    const request = TestBed.inject(HttpTestingController).expectOne('http://localhost:5213/api/tickets/55/comments');
    expect(request.request.body).toEqual({ comment: 'Gracias por la atención' });
    request.flush(null, { status: 204, statusText: 'No Content' });
    TestBed.inject(HttpTestingController).expectNone('http://localhost:5213/api/users');
  });

  it('enforces the three-assignee limit and hides management actions on terminal tickets', () => {
    const fixture = render('Operator', { ...ticket, assignees: [assignee, { ...assignee, userId: 3 }, { ...assignee, userId: 4 }] });
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.add-operator')).toBeNull();
    expect(element.textContent).toContain('Máximo de tres');
    fixture.componentRef.setInput('ticket', { ...ticket, status: 'Rejected' });
    fixture.detectChanges();
    expect(element.querySelector('.button-row')).toBeNull();
    expect(element.querySelector('.comment-form')).not.toBeNull();
  });
});
