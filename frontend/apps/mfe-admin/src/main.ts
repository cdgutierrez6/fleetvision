import { initFederation } from '@angular-architects/native-federation';

initFederation()
  .catch(err => console.error('[mfe-admin] Federation init error:', err))
  .then(() => import('./bootstrap'))
  .catch(err => console.error('[mfe-admin] Bootstrap error:', err));
