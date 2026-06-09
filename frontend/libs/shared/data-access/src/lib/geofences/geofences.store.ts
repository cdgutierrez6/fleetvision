import { inject } from '@angular/core';
import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setEntities } from '@ngrx/signals/entities';
import { firstValueFrom } from 'rxjs';
import { Geofence } from '@fleetvision/shared/models';
import { GeofencesService } from './geofences.service';

export const GeofencesStore = signalStore(
  { providedIn: 'root' },
  withEntities<Geofence>(),
  withState({ isLoading: false }),
  withMethods((store, service = inject(GeofencesService)) => ({
    async loadAll(): Promise<void> {
      patchState(store, { isLoading: true });
      try {
        const geofences = await firstValueFrom(service.getAll());
        patchState(store, setEntities(geofences), { isLoading: false });
      } catch {
        patchState(store, { isLoading: false });
      }
    },
  }))
);
