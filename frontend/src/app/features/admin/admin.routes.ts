import { Routes } from '@angular/router';
import { areaByPath } from '../../core/auth/areas';
import { ComingSoon } from '../../shared/ui/coming-soon';

const area = areaByPath('admin');

export const routes: Routes = [
  {
    path: '',
    component: ComingSoon,
    title: `${area.label} · HR Service Desk`,
    data: { title: area.label, description: area.description, phase: 5 },
  },
];
