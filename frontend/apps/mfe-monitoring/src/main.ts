import { initFederation } from '@angular-architects/native-federation';

initFederation()
  .catch(err => console.error('[mfe-monitoring] Federation init error:', err))
  .then(() => import('./bootstrap'))
  .catch(err => console.error('[mfe-monitoring] Bootstrap error:', err));
