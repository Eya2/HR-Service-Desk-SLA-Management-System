import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, InjectionToken, computed, effect, inject, signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Subscription, interval, startWith, switchMap } from 'rxjs';
import { Paged } from '../api/api.models';
import { AuthService } from '../auth/auth.service';

export interface AppNotification {
  id: string;
  type: string;
  title: string;
  message: string;
  ticketId: string | null;
  createdAt: string;
  isRead: boolean;
}

/** The live connection: anything that can register a handler, start and stop. */
export interface NotificationHub {
  on(method: string, handler: (notification: AppNotification) => void): void;
  start(): Promise<void>;
  stop(): Promise<void>;
}

/** Builds the SignalR connection; tests replace it with an inert fake. */
export const NOTIFICATION_HUB = new InjectionToken<(accessToken: () => string) => NotificationHub>('NOTIFICATION_HUB', {
  providedIn: 'root',
  factory: () => (accessToken) =>
    new HubConnectionBuilder()
      .withUrl('/hubs/notifications', { accessTokenFactory: accessToken })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build(),
});

/** How often the unread count is refreshed when the live connection is down. */
export const POLL_INTERVAL_MS = 60_000;

/**
 * Notifications of the signed-in user: pushed live over SignalR, with polling as a fallback.
 * Starts on sign-in and stops on sign-out.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly snackBar = inject(MatSnackBar);

  private readonly items = signal<AppNotification[]>([]);
  private readonly unread = signal(0);
  private readonly createHub = inject(NOTIFICATION_HUB);
  private connection: NotificationHub | null = null;
  private polling: Subscription | null = null;

  readonly latest = this.items.asReadonly();
  readonly unreadCount = this.unread.asReadonly();
  readonly hasUnread = computed(() => this.unread() > 0);

  constructor() {
    effect(() => (this.auth.isAuthenticated() ? this.start() : this.stop()));
    inject(DestroyRef).onDestroy(() => this.stop());
  }

  refresh(): void {
    this.http.get<Paged<AppNotification>>('/api/notifications', { params: { pageSize: 10 } }).subscribe((page) => this.items.set(page.items));
    this.http.get<number>('/api/notifications/unread-count').subscribe((count) => this.unread.set(count));
  }

  markRead(notification: AppNotification): void {
    if (notification.isRead) return;
    this.http.post<void>(`/api/notifications/${encodeURIComponent(notification.id)}/read`, null).subscribe(() => {
      this.items.update((list) => list.map((n) => (n.id === notification.id ? { ...n, isRead: true } : n)));
      this.unread.update((count) => Math.max(0, count - 1));
    });
  }

  markAllRead(): void {
    this.http.post<void>('/api/notifications/read-all', null).subscribe(() => {
      this.items.update((list) => list.map((n) => ({ ...n, isRead: true })));
      this.unread.set(0);
    });
  }

  /** Called for every notification pushed by the server. */
  receive(notification: AppNotification): void {
    this.items.update((list) => [notification, ...list.filter((n) => n.id !== notification.id)].slice(0, 10));
    this.unread.update((count) => count + 1);
    this.snackBar.open(notification.title, 'OK', { duration: 5000 });
  }

  private start(): void {
    if (this.polling) return;
    this.polling = interval(POLL_INTERVAL_MS)
      .pipe(
        startWith(0),
        switchMap(() => this.http.get<number>('/api/notifications/unread-count')),
      )
      .subscribe((count) => this.unread.set(count));
    this.refresh();
    void this.connect();
  }

  private stop(): void {
    this.polling?.unsubscribe();
    this.polling = null;
    void this.connection?.stop();
    this.connection = null;
    this.items.set([]);
    this.unread.set(0);
  }

  private async connect(): Promise<void> {
    const connection = this.createHub(() => this.auth.accessToken() ?? '');
    connection.on('notification', (n: AppNotification) => this.receive(n));
    this.connection = connection;
    try {
      await connection.start();
    } catch {
      // Polling keeps the count fresh until the next sign-in.
    }
  }
}
