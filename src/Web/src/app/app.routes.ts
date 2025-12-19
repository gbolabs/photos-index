import { Routes } from '@angular/router';
import { Dashboard } from './features/dashboard/dashboard';
import { Duplicates } from './features/duplicates/duplicates';
import { Settings } from './features/settings/settings';

export const routes: Routes = [
  { path: '', component: Dashboard },
  { path: 'duplicates', component: Duplicates },
  { path: 'settings', component: Settings },
  { path: '**', redirectTo: '' }
];
