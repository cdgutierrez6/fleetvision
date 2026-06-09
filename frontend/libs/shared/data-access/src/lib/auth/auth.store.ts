import { computed } from '@angular/core';
import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { AuthUser } from '@fleetvision/shared/models';

export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState({
    accessToken: null as string | null,
    user: null as AuthUser | null,
  }),
  withComputed(({ accessToken, user }) => ({
    isAuthenticated: computed(() => !!accessToken() && !!user()),
    tenantId: computed(() => user()?.tenantId ?? null),
    tenantName: computed(() => user()?.tenantName ?? ''),
    displayName: computed(() => user()?.name ?? user()?.email ?? ''),
    avatarInitial: computed(() => {
      const name = user()?.name ?? user()?.email ?? '?';
      return name.charAt(0).toUpperCase();
    }),
  })),
  withMethods((store) => ({
    setSession(accessToken: string, user: AuthUser): void {
      patchState(store, { accessToken, user });
    },
    clearSession(): void {
      patchState(store, { accessToken: null, user: null });
    },
  }))
);

export type AuthStoreType = InstanceType<typeof AuthStore>;
