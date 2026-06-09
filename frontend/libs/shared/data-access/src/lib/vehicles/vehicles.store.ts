import { computed, inject } from '@angular/core';
import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setEntities } from '@ngrx/signals/entities';
import { firstValueFrom } from 'rxjs';
import { Vehicle } from '@fleetvision/shared/models';
import { VehiclesService } from './vehicles.service';

export const VehiclesStore = signalStore(
  { providedIn: 'root' },
  withEntities<Vehicle>(),
  withState({ isLoading: false, error: null as string | null }),
  withComputed(({ entities }) => ({
    totalCount: computed(() => entities().length),
    activeCount: computed(() => entities().filter(v => v.status === 'Active').length),
    maintenanceCount: computed(() => entities().filter(v => v.status === 'Maintenance').length),
    inactiveCount: computed(() => entities().filter(v => v.status === 'Inactive').length),
    vehiclesWithCoords: computed(() =>
      entities().filter(v => v.currentLatitude != null && v.currentLongitude != null)
    ),
  })),
  withMethods((store, service = inject(VehiclesService)) => ({
    async loadAll(): Promise<void> {
      patchState(store, { isLoading: true, error: null });
      try {
        const vehicles = await firstValueFrom(service.getAll());
        patchState(store, setEntities(vehicles), { isLoading: false });
      } catch {
        patchState(store, { isLoading: false, error: 'Error al cargar vehículos' });
      }
    },
  }))
);
