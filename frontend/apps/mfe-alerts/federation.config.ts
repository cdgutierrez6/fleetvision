import { FederationConfig, withNativeFederation, shareAll } from '@angular-architects/native-federation';

const config: FederationConfig = {
  name: 'mfe-alerts',
  exposes: {
    './Feed': './apps/mfe-alerts/src/app/feed/feed.component.ts',
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
