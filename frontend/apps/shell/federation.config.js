const { withNativeFederation, shareAll } = require('@angular-architects/native-federation/config');

module.exports = withNativeFederation({
  name: 'shell',
  remotes: {
    'mfe-fleet':      'http://localhost:4201/remoteEntry.json',
    'mfe-alerts':     'http://localhost:4202/remoteEntry.json',
    'mfe-monitoring': 'http://localhost:4203/remoteEntry.json',
    'mfe-admin':      'http://localhost:4204/remoteEntry.json',
    'mfe-reports':    'http://localhost:4205/remoteEntry.json',
    'mfe-billing':    'http://localhost:4206/remoteEntry.json',
  },
  shared: {
    ...shareAll({ singleton: true, strictVersion: true, requiredVersion: 'auto' }),
  },
  skip: [
    'rxjs/ajax', 'rxjs/fetch', 'rxjs/testing', 'rxjs/webSocket',
    '@angular/compiler',
  ],
});
