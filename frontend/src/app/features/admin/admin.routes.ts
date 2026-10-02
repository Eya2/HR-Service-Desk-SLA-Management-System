import { Routes } from '@angular/router';
import { AdminLayout } from './admin-layout';

export const routes: Routes = [
  {
    path: '',
    component: AdminLayout,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'workflows' },
      {
        path: 'workflows',
        loadComponent: () => import('./workflows-list').then((m) => m.WorkflowsList),
        title: 'Approval workflows · HR Service Desk',
      },
      {
        path: 'workflows/:requestTypeId',
        loadComponent: () => import('./workflow-editor').then((m) => m.WorkflowEditor),
        title: 'Edit workflow · HR Service Desk',
      },
    ],
  },
];
