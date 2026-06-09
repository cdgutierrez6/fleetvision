export const environment = {
  production: false,
  apiUrl: 'http://localhost:8080',
  signalRHubUrl: 'http://localhost:8080/hubs/violations',
  oidc: {
    issuer: 'http://localhost:8080',
    clientId: 'fleetvision-spa',
    redirectUri: 'http://localhost:4200/auth/callback',
  },
};
