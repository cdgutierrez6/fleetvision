export const environment = {
  production: true,
  apiUrl: 'https://api.fleetvision.com',
  signalRHubUrl: 'https://api.fleetvision.com/hubs/violations',
  oidc: {
    issuer: 'https://api.fleetvision.com',
    clientId: 'fleetvision-spa',
    redirectUri: 'https://app.fleetvision.com/auth/callback',
  },
};
