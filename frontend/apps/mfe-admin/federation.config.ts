import { FederationConfig, withNativeFederation, shareAll } from '@angular-architects/native-federation';

const config: FederationConfig = {
  name: 'mfe-admin',
  exposes: {
    './Admin': './apps/mfe-admin/src/app/admin/admin.component.ts',
  },
  shared: {
    ...shareAll({
      singleton: true,
      strictVersion: true,
      requiredVersion: 'auto',
    }),
  },
  skip: [
    'rxjs/ajax', 'rxjs/fetch', 'rxjs/testing', 'rxjs/webSocket',
    '@angular/compiler',
  ],
};

export default withNativeFederation(config);
