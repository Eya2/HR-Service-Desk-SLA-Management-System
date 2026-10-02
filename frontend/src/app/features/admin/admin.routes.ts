import { Routes } from '@angular/router';
import { AdminLayout } from './admin-layout';

export const routes: Routes = [
  {
    path: '',
    component: AdminLayout,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'users' },
      {
        path: 'users',
        loadComponent: () => import('./users-admin').then((m) => m.UsersAdmin),
        title: 'title.users',
      },
      {
        path: 'workflows',
        loadComponent: () => import('./workflows-list').then((m) => m.WorkflowsList),
        title: 'title.workflows',
      },
      {
        path: 'teams',
        loadComponent: () => import('./teams-admin').then((m) => m.TeamsAdmin),
        title: 'title.teams',
      },
      {
        path: 'sla',
        loadComponent: () => import('./sla-admin').then((m) => m.SlaAdmin),
        title: 'title.sla',
      },
      {
        path: 'escalations',
        loadComponent: () => import('./escalations-admin').then((m) => m.EscalationsAdmin),
        title: 'title.escalations',
      },
      {
        path: 'retention',
        loadComponent: () => import('./retention-admin').then((m) => m.RetentionAdmin),
        title: 'title.retention',
      },
      {
        path: 'knowledge',
        loadComponent: () => import('./knowledge-admin').then((m) => m.KnowledgeAdmin),
        title: 'title.kbAdmin',
      },
      {
        path: 'integrations',
        loadComponent: () => import('./integrations-admin').then((m) => m.IntegrationsAdmin),
        title: 'title.integrations',
      },
      {
        path: 'workflows/:requestTypeId',
        loadComponent: () => import('./workflow-editor').then((m) => m.WorkflowEditor),
        title: 'title.editWorkflow',
      },
    ],
  },
];
