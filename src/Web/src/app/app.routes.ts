import { Routes } from '@angular/router';
import { Dashboard } from './features/dashboard/dashboard';
import { Duplicates } from './features/duplicates/duplicates';
import { Settings } from './features/settings/settings';
import { Indexing } from './features/indexing/indexing';
import { Files } from './features/files/files';
import { FileDetailComponent } from './features/files/file-detail/file-detail.component';

export const routes: Routes = [
  { path: '', component: Dashboard },
  { path: 'indexing', component: Indexing },
  { path: 'files', component: Files },
  { path: 'files/:id', component: FileDetailComponent },
  { path: 'duplicates', component: Duplicates },
  { path: 'settings', component: Settings },
  { path: '**', redirectTo: '' }
];
