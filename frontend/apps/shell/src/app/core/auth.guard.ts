import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from '@fleetvision/shared/data-access';

export const authGuard: CanActivateFn = () => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  if (authStore.isAuthenticated()) return true;
  return router.createUrlTree(['/login']);
};

// Prevents authenticated users from accessing public-only routes (login, register).
export const publicGuard: CanActivateFn = () => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  if (!authStore.isAuthenticated()) return true;
  return router.createUrlTree(['/fleet']);
};
