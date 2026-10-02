import { Routes } from '@angular/router';
import { roleGuard } from '../../core/auth/auth.guards';
import { ManagerLayout } from './manager-layout';

export const routes: Routes = [
  {
    path: '',
    component: ManagerLayout,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'queue' },
      {
        path: 'queue',
        loadComponent: () => import('./approvals-queue').then((m) => m.ApprovalsQueue),
        title: 'title.approvals',
      },
      {
        path: 'team',
        canActivate: [roleGuard(['Manager'])],
        loadComponent: () => import('./team').then((m) => m.Team),
        title: 'title.team',
      },
    ],
  },
];
