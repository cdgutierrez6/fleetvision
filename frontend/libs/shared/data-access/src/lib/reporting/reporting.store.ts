import { inject } from '@angular/core';
import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { computed } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  ReportingService,
  FleetKpisDto,
  ViolationsSummaryDto,
  FleetStatusDto,
  DateRange,
} from './reporting.service';

interface ReportingState {
  fleetKpis:        FleetKpisDto | null;
  violationsSummary: ViolationsSummaryDto | null;
  fleetStatus:       FleetStatusDto | null;
  isLoading:         boolean;
  isExporting:       boolean;
  error:             string | null;
  range:             DateRange;
}

const initialState: ReportingState = {
  fleetKpis:         null,
  violationsSummary: null,
  fleetStatus:       null,
  isLoading:         false,
  isExporting:       false,
  error:             null,
  range:             '30d',
};

export const ReportingStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ fleetKpis, violationsSummary, fleetStatus }) => ({
    hasData: computed(() => fleetKpis() !== null),
    totalViolations: computed(() => violationsSummary()?.totalViolations ?? 0),
    activeVehicles: computed(() => fleetKpis()?.activeVehicles ?? 0),
    totalDistanceKm: computed(() => fleetKpis()?.totalDistanceKm ?? 0),
    avgSpeedKmh: computed(() => fleetKpis()?.avgSpeedKmh ?? 0),
    fleetTotal: computed(() => fleetStatus()?.total ?? 0),
  })),
  withMethods((store, service = inject(ReportingService)) => ({
    setRange(range: DateRange): void {
      patchState(store, { range });
    },

    async loadAll(): Promise<void> {
      const range = store.range();
      patchState(store, { isLoading: true, error: null });
      try {
        const [kpis, violations, status] = await Promise.all([
          firstValueFrom(service.getFleetKpis(range)),
          firstValueFrom(service.getViolationsSummary(range)),
          firstValueFrom(service.getFleetStatus()),
        ]);
        patchState(store, {
          fleetKpis:         kpis,
          violationsSummary: violations,
          fleetStatus:       status,
          isLoading:         false,
        });
      } catch (err) {
        patchState(store, {
          isLoading: false,
          error: 'Error al cargar reportes. Intenta nuevamente.',
        });
      }
    },

    async exportPdf(tenantName: string): Promise<void> {
      patchState(store, { isExporting: true });
      try {
        const blob = await firstValueFrom(service.exportPdf(tenantName, store.range()));
        const url  = URL.createObjectURL(blob);
        const a    = document.createElement('a');
        a.href     = url;
        a.download = `fleetvision-report-${store.range()}.pdf`;
        a.click();
        URL.revokeObjectURL(url);
      } catch {
        patchState(store, { error: 'Error al exportar PDF.' });
      } finally {
        patchState(store, { isExporting: false });
      }
    },
  }))
);
