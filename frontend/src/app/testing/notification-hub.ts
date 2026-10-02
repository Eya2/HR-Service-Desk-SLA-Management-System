import { NOTIFICATION_HUB, NotificationHub } from '../core/notifications/notification.service';

/** An inert hub: specs never open a real SignalR connection. */
export const provideFakeNotificationHub = () => ({
  provide: NOTIFICATION_HUB,
  useValue: (): NotificationHub => ({ on: () => undefined, start: () => Promise.resolve(), stop: () => Promise.resolve() }),
});
