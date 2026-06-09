import { computed, inject } from '@angular/core';
import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setEntities, updateEntity } from '@ngrx/signals/entities';
import { firstValueFrom } from 'rxjs';
import { TenantProfile, PlanTier } from '@fleetvision/shared/models';
import { TenantsService } from './tenants.service';

export const TenantsStore = signalStore(
  { providedIn: 'root' },
  withEntities<TenantProfile>(),
  withState({ isLoading: false, isSaving: false, error: null as string | null }),
  withComputed(({ entities }) => ({
    totalCount:    computed(() => entities().length),
    activeCount:   computed(() => entities().filter(t => t.isActive).length),
    inactiveCount: computed(() => entities().filter(t => !t.isActive).length),
    byPlan: computed(() => ({
      Free:         entities().filter(t => t.planTier === 'Free').length,
      Starter:      entities().filter(t => t.planTier === 'Starter').length,
      Professional: entities().filter(t => t.planTier === 'Professional').length,
      Enterprise:   entities().filter(t => t.planTier === 'Enterprise').length,
    })),
  })),
  withMethods((store, service = inject(TenantsService)) => ({
    async loadAll(): Promise<void> {
      patchState(store, { isLoading: true, error: null });
      try {
        const tenants = await firstValueFrom(service.getAll());
        patchState(store, setEntities(tenants), { isLoading: false });
      } catch {
        patchState(store, { isLoading: false, error: 'Error al cargar tenants' });
      }
    },

    async updatePlan(id: string, planTier: PlanTier): Promise<void> {
      patchState(store, { isSaving: true, error: null });
      try {
        await firstValueFrom(service.updatePlan(id, { planTier }));
        patchState(store, updateEntity({ id, changes: { planTier } }), { isSaving: false });
      } catch {
        patchState(store, { isSaving: false, error: 'Error al actualizar plan' });
        throw new Error('updatePlan failed');
      }
    },

    async setActive(id: string, isActive: boolean): Promise<void> {
      patchState(store, { isSaving: true, error: null });
      try {
        await firstValueFrom(service.setActive(id, isActive));
        patchState(store, updateEntity({ id, changes: { isActive } }), { isSaving: false });
      } catch {
        patchState(store, { isSaving: false, error: 'Error al cambiar estado del tenant' });
        throw new Error('setActive failed');
      }
    },
  }))
);
