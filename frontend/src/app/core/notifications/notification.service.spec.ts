import { provideFakeNotificationHub } from '../../testing/notification-hub';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { session } from '../../testing/auth-fixtures';
import { AppNotification, NotificationService } from './notification.service';

describe('NotificationService', () => {
  let service: NotificationService;
  let http: HttpTestingController;

  const note = (id: string, isRead = false): AppNotification => ({
    id,
    type: 'CommentAdded',
    title: `HR-2026-00000${id} new message`,
    message: 'There is a new message.',
    ticketId: 't-1',
    createdAt: '2026-03-16T09:00:00Z',
    isRead,
  });

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideFakeNotificationHub()] });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(NotificationService);
    TestBed.inject(AuthService).login('a@acme.example', 'x').subscribe();
    http.expectOne('/api/auth/login').flush(session());
    TestBed.tick(); // sign-in starts the service

    for (const req of http.match('/api/notifications/unread-count')) req.flush(2);
    http.expectOne((r) => r.url === '/api/notifications').flush({ items: [note('1'), note('2')], page: 1, pageSize: 10, totalCount: 2 });
  });

  it('loads the latest notifications and the unread count on sign-in', () => {
    expect(service.latest().length).toBe(2);
    expect(service.unreadCount()).toBe(2);
  });

  it('adds pushed notifications on top and counts them', () => {
    service.receive(note('3'));

    expect(service.latest()[0].id).toBe('3');
    expect(service.unreadCount()).toBe(3);
  });

  it('marks one or all notifications as read', () => {
    service.markRead(service.latest()[0]);
    http.expectOne('/api/notifications/1/read').flush(null);
    expect(service.unreadCount()).toBe(1);

    service.markAllRead();
    http.expectOne('/api/notifications/read-all').flush(null);
    expect(service.unreadCount()).toBe(0);
    expect(service.latest().every((n) => n.isRead)).toBeTrue();
  });

  it('clears everything on sign-out', () => {
    TestBed.inject(AuthService).logout();
    http.expectOne('/api/auth/logout').flush(null);
    TestBed.tick();

    expect(service.latest()).toEqual([]);
    expect(service.unreadCount()).toBe(0);
  });
});
