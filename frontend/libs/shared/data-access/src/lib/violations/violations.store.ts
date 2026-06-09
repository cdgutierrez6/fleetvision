import { computed } from '@angular/core';
import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { ViolationEvent } from '@fleetvision/shared/models';

const MAX_VIOLATIONS = 50;

export const ViolationsStore = signalStore(
  { providedIn: 'root' },
  withState({
    violations: [] as ViolationEvent[],
    unreadCount: 0,
    isConnected: false,
  }),
  withComputed(({ violations }) => ({
    latestViolation: computed(() => violations()[0] ?? null),
    violationsToday: computed(() => {
      const today = new Date().toDateString();
      return violations().filter(
        v => new Date(v.occurredAt).toDateString() === today
      ).length;
    }),
  })),
  withMethods((store) => ({
    append(event: ViolationEvent): void {
      const updated = [event, ...store.violations()].slice(0, MAX_VIOLATIONS);
      patchState(store, {
        violations: updated,
        unreadCount: store.unreadCount() + 1,
      });
    },
    markAllRead(): void {
      patchState(store, { unreadCount: 0 });
    },
    setConnected(isConnected: boolean): void {
      patchState(store, { isConnected });
    },
  }))
);
