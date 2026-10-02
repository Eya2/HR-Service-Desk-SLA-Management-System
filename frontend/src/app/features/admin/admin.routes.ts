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
        path: 'teams',
        loadComponent: () => import('./teams-admin').then((m) => m.TeamsAdmin),
        title: 'Teams · HR Service Desk',
      },
      {
        path: 'sla',
        loadComponent: () => import('./sla-admin').then((m) => m.SlaAdmin),
        title: 'SLA & calendar · HR Service Desk',
      },
      {
        path: 'escalations',
        loadComponent: () => import('./escalations-admin').then((m) => m.EscalationsAdmin),
        title: 'Escalation rules · HR Service Desk',
      },
      {
        path: 'workflows/:requestTypeId',
        loadComponent: () => import('./workflow-editor').then((m) => m.WorkflowEditor),
        title: 'Edit workflow · HR Service Desk',
      },
    ],
  },
];
