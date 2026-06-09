import { initFederation } from '@angular-architects/native-federation';

initFederation()
  .catch(err => console.error('[mfe-alerts] Federation init error:', err))
  .then(() => import('./bootstrap'))
  .catch(err => console.error('[mfe-alerts] Bootstrap error:', err));
