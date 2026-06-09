import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  OnDestroy,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import type * as L from 'leaflet';
import { Vehicle, Geofence, ViolationEvent, VIOLATION_TYPE_LABELS } from '@fleetvision/shared/models';
import { VehiclesStore, ViolationsStore, GeofencesStore } from '@fleetvision/shared/data-access';
import { StatusBadgeComponent } from '@fleetvision/shared/ui';

type LeafletLib = typeof L;

@Component({
  selector: 'fv-map',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    StatusBadgeComponent,
  ],
  styles: [`
    :host { display: flex; height: 100%; overflow: hidden; }

    .map-wrapper {
      position: relative;
      flex: 1;
      display: flex;
    }

    #fv-leaflet-map {
      flex: 1;
      min-height: 0;
    }

    /* Map toolbar overlay */
    .map-toolbar {
      position: absolute;
      top: 12px;
      left: 12px;
      z-index: 1000;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .toolbar-card {
      background: #fff;
      border-radius: 8px;
      border: 1px solid var(--fv-border, #E0E4EA);
      box-shadow: 0 2px 8px rgba(0,0,0,.1);
      padding: 10px 12px;
    }
    .toolbar-title { font-size: 11px; font-weight: 700; color: var(--fv-text-muted, #6B7280); text-transform: uppercase; letter-spacing: .5px; margin-bottom: 6px; }
    .toggle-row { display: flex; align-items: center; gap: 8px; font-size: 12px; cursor: pointer; }
    .toggle-row input[type="checkbox"] { cursor: pointer; accent-color: #1E3A5F; }

    /* Legend */
    .legend {
      position: absolute;
      bottom: 28px;
      left: 12px;
      z-index: 1000;
      background: #fff;
      border-radius: 8px;
      border: 1px solid var(--fv-border, #E0E4EA);
      box-shadow: 0 2px 8px rgba(0,0,0,.1);
      padding: 12px;
      min-width: 160px;
    }
    .legend-title { font-size: 11px; font-weight: 700; color: var(--fv-text-muted, #6B7280); text-transform: uppercase; letter-spacing: .5px; margin-bottom: 8px; }
    .legend-item { display: flex; align-items: center; gap: 8px; font-size: 12px; margin-bottom: 4px; }
    .legend-dot { width: 10px; height: 10px; border-radius: 50%; flex-shrink: 0; }
    .legend-rect { width: 14px; height: 10px; border-radius: 2px; flex-shrink: 0; opacity: .6; }

    /* Vehicle info panel */
    .vehicle-panel {
      width: 300px;
      background: #fff;
      border-left: 1px solid var(--fv-border, #E0E4EA);
      overflow-y: auto;
      display: flex;
      flex-direction: column;
    }
    .panel-header {
      padding: 16px;
      background: var(--fv-primary, #1E3A5F);
      color: #fff;
    }
    .panel-plate { font-size: 18px; font-weight: 700; font-family: 'Courier New', monospace; letter-spacing: 1px; }
    .panel-make { font-size: 12px; opacity: .8; margin-top: 2px; }
    .panel-body { padding: 16px; flex: 1; }
    .info-row { display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #F3F4F6; font-size: 13px; }
    .info-label { color: var(--fv-text-muted, #6B7280); }
    .info-value { font-weight: 500; }
    .panel-section-title { font-size: 12px; font-weight: 700; color: var(--fv-primary, #1E3A5F); text-transform: uppercase; letter-spacing: .5px; margin: 16px 0 8px; }
    .violation-item { padding: 8px; background: #FEF2F2; border-radius: 6px; margin-bottom: 6px; font-size: 12px; border-left: 3px solid #F44336; }
    .violation-type { font-weight: 600; color: #DC2626; }
    .violation-time { color: #9E9E9E; margin-top: 2px; }
    .close-btn { cursor: pointer; }

    /* Stats bar at top right */
    .stats-overlay {
      position: absolute;
      top: 12px;
      right: 12px;
      z-index: 1000;
      display: flex;
      gap: 8px;
    }
    .stat-pill {
      background: #fff;
      border-radius: 20px;
      border: 1px solid var(--fv-border, #E0E4EA);
      box-shadow: 0 2px 8px rgba(0,0,0,.1);
      padding: 6px 12px;
      font-size: 12px;
      font-weight: 600;
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .stat-dot { width: 8px; height: 8px; border-radius: 50%; }

    /* Loading overlay */
    .loading-overlay {
      position: absolute;
      inset: 0;
      z-index: 2000;
      background: rgba(255,255,255,.7);
      display: flex;
      align-items: center;
      justify-content: center;
      flex-direction: column;
      gap: 12px;
      font-size: 14px;
      color: var(--fv-text-muted, #6B7280);
    }
  `],
  template: `
    <div class="map-wrapper">

      @if (isInitializing()) {
        <div class="loading-overlay">
          <mat-spinner [diameter]="40" color="accent"></mat-spinner>
          <span>Cargando mapa...</span>
        </div>
      }

      <!-- Map container -->
      <div id="fv-leaflet-map" #mapContainer></div>

      <!-- Toolbar -->
      <div class="map-toolbar">
        <div class="toolbar-card">
          <div class="toolbar-title">Capas</div>
          <label class="toggle-row">
            <input type="checkbox" [checked]="showVehicles()" (change)="showVehicles.set(!showVehicles())" />
            Vehículos ({{ vehiclesWithCoords().length }})
          </label>
          <label class="toggle-row" style="margin-top:4px">
            <input type="checkbox" [checked]="showGeofences()" (change)="showGeofences.set(!showGeofences())" />
            Geofences ({{ geofencesStore.entities().length }})
          </label>
          <label class="toggle-row" style="margin-top:4px">
            <input type="checkbox" [checked]="showViolations()" (change)="showViolations.set(!showViolations())" />
            Alertas recientes
          </label>
        </div>

        <div class="toolbar-card">
          <div class="toolbar-title">Acciones</div>
          <button mat-stroked-button style="width:100%;font-size:12px;" (click)="fitBounds()">
            <mat-icon style="font-size:16px">fit_screen</mat-icon>
            Ajustar vista
          </button>
          <button mat-stroked-button style="width:100%;font-size:12px;margin-top:6px" (click)="refresh()">
            <mat-icon style="font-size:16px">refresh</mat-icon>
            Actualizar datos
          </button>
        </div>
      </div>

      <!-- Stats overlay top right -->
      <div class="stats-overlay">
        <div class="stat-pill">
          <span class="stat-dot" style="background:#4CAF50"></span>
          {{ activeVehiclesOnMap() }} activos
        </div>
        <div class="stat-pill">
          <span class="stat-dot" style="background:#F44336"></span>
          {{ violationsStore.violationsToday() }} alertas hoy
        </div>
      </div>

      <!-- Legend -->
      <div class="legend">
        <div class="legend-title">Leyenda</div>
        <div class="legend-item">
          <div class="legend-dot" style="background:#4CAF50"></div>
          <span>Vehículo activo</span>
        </div>
        <div class="legend-item">
          <div class="legend-dot" style="background:#FF9800"></div>
          <span>Mantenimiento</span>
        </div>
        <div class="legend-item">
          <div class="legend-dot" style="background:#9E9E9E"></div>
          <span>Inactivo</span>
        </div>
        <div class="legend-item">
          <div class="legend-dot" style="background:#F44336;animation: pulse-red 1.5s infinite"></div>
          <span>Violación reciente</span>
        </div>
        <div class="legend-item">
          <div class="legend-rect" style="background:#2196F3;border:1px solid #1976D2"></div>
          <span>Zona permitida</span>
        </div>
        <div class="legend-item">
          <div class="legend-rect" style="background:#F44336;border:1px solid #D32F2F"></div>
          <span>Zona prohibida</span>
        </div>
      </div>

    </div>

    <!-- Vehicle detail panel -->
    @if (selectedVehicle()) {
      <div class="vehicle-panel">
        <div class="panel-header">
          <div style="display:flex;justify-content:space-between;align-items:flex-start">
            <div>
              <div class="panel-plate">{{ selectedVehicle()!.plateNumber }}</div>
              <div class="panel-make">{{ selectedVehicle()!.make }} {{ selectedVehicle()!.model }}</div>
            </div>
            <button mat-icon-button class="close-btn" style="color:#fff" (click)="selectedVehicle.set(null)">
              <mat-icon>close</mat-icon>
            </button>
          </div>
        </div>
        <div class="panel-body">
          <fv-status-badge [status]="selectedVehicle()!.status" />

          <div class="info-row">
            <span class="info-label">Año</span>
            <span class="info-value">{{ selectedVehicle()!.year }}</span>
          </div>
          <div class="info-row">
            <span class="info-label">Latitud</span>
            <span class="info-value">{{ selectedVehicle()!.currentLatitude | number:'1.4-4' }}</span>
          </div>
          <div class="info-row">
            <span class="info-label">Longitud</span>
            <span class="info-value">{{ selectedVehicle()!.currentLongitude | number:'1.4-4' }}</span>
          </div>
          <div class="info-row">
            <span class="info-label">Última señal</span>
            <span class="info-value">{{ selectedVehicle()!.lastSeenAt ? (selectedVehicle()!.lastSeenAt | date:'HH:mm:ss') : '—' }}</span>
          </div>

          <div class="panel-section-title">Violaciones recientes</div>
          @if (selectedVehicleViolations().length === 0) {
            <p style="font-size:12px;color:#9E9E9E;text-align:center;padding:12px 0">Sin violaciones recientes</p>
          } @else {
            @for (v of selectedVehicleViolations(); track v.id) {
              <div class="violation-item">
                <div class="violation-type">{{ typeLabels[v.violationType] }}</div>
                @if (v.actualSpeedKmh) {
                  <div>{{ v.actualSpeedKmh }} km/h (límite: {{ v.limitSpeedKmh }})</div>
                }
                <div class="violation-time">{{ v.occurredAt | date:'dd/MM HH:mm' }} — {{ v.geofenceName }}</div>
              </div>
            }
          }
        </div>
      </div>
    }
  `,
})
export class MapComponent implements OnInit, OnDestroy {
  readonly vehiclesStore = inject(VehiclesStore);
  readonly violationsStore = inject(ViolationsStore);
  readonly geofencesStore = inject(GeofencesStore);

  mapContainerRef = viewChild<ElementRef<HTMLDivElement>>('mapContainer');

  selectedVehicle = signal<Vehicle | null>(null);
  showVehicles = signal(true);
  showGeofences = signal(true);
  showViolations = signal(true);
  isInitializing = signal(true);

  vehiclesWithCoords = computed(() =>
    this.vehiclesStore.entities().filter(v => v.currentLatitude != null && v.currentLongitude != null)
  );

  activeVehiclesOnMap = computed(() =>
    this.vehiclesWithCoords().filter(v => v.status === 'Active').length
  );

  selectedVehicleViolations = computed(() => {
    const v = this.selectedVehicle();
    if (!v) return [];
    return this.violationsStore.violations().filter(ev => ev.vehicleId === v.id).slice(0, 5);
  });

  readonly typeLabels = VIOLATION_TYPE_LABELS;

  private map: L.Map | null = null;
  private L: LeafletLib | null = null;
  private vehicleMarkers = new Map<string, L.Marker>();
  private geofencePolygons = new Map<string, L.Polygon>();
  private violationMarkers: L.CircleMarker[] = [];

  constructor() {
    afterNextRender(() => {
      this.initMap();
    });

    effect(() => {
      const vehicles = this.vehiclesWithCoords();
      const show = this.showVehicles();
      if (this.map && this.L) this.updateVehicleMarkers(vehicles, show);
    });

    effect(() => {
      const geofences = this.geofencesStore.entities();
      const show = this.showGeofences();
      if (this.map && this.L) this.updateGeofencePolygons(geofences, show);
    });

    effect(() => {
      const violations = this.violationsStore.violations();
      const show = this.showViolations();
      if (this.map && this.L) this.updateViolationMarkers(violations, show);
    });
  }

  ngOnInit(): void {
    const promises: Promise<unknown>[] = [];
    if (this.vehiclesStore.totalCount() === 0) promises.push(this.vehiclesStore.loadAll());
    if (this.geofencesStore.entities().length === 0) promises.push(this.geofencesStore.loadAll());
    Promise.all(promises);
  }

  ngOnDestroy(): void {
    if (this.map) {
      this.map.remove();
      this.map = null;
    }
  }

  private async initMap(): Promise<void> {
    const L = await import('leaflet');
    this.L = L;

    const el = this.mapContainerRef()?.nativeElement;
    if (!el) return;

    this.map = L.map(el, {
      center: [4.711, -74.0721],
      zoom: 11,
      zoomControl: true,
    });

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
      maxZoom: 19,
    }).addTo(this.map);

    this.isInitializing.set(false);

    this.updateVehicleMarkers(this.vehiclesWithCoords(), this.showVehicles());
    this.updateGeofencePolygons(this.geofencesStore.entities(), this.showGeofences());
    this.updateViolationMarkers(this.violationsStore.violations(), this.showViolations());
  }

  private getVehicleColor(status: Vehicle['status']): string {
    return status === 'Active' ? '#4CAF50' : status === 'Maintenance' ? '#FF9800' : '#9E9E9E';
  }

  private makeVehicleIcon(L: LeafletLib, vehicle: Vehicle): L.DivIcon {
    const color = this.getVehicleColor(vehicle.status);
    return L.divIcon({
      className: '',
      html: `
        <div style="
          background:${color};
          width:34px;height:34px;border-radius:50% 50% 50% 0;
          transform:rotate(-45deg);border:3px solid #fff;
          box-shadow:0 2px 8px rgba(0,0,0,.3);display:flex;align-items:center;justify-content:center;">
          <span style="transform:rotate(45deg);font-size:14px;color:#fff;font-weight:700;">🚛</span>
        </div>`,
      iconSize: [34, 34],
      iconAnchor: [17, 34],
      popupAnchor: [0, -36],
    });
  }

  private updateVehicleMarkers(vehicles: Vehicle[], show: boolean): void {
    if (!this.map || !this.L) return;

    this.vehicleMarkers.forEach(m => m.remove());
    this.vehicleMarkers.clear();

    if (!show) return;

    vehicles.forEach(v => {
      if (v.currentLatitude == null || v.currentLongitude == null) return;

      const marker = this.L!.marker([v.currentLatitude, v.currentLongitude], {
        icon: this.makeVehicleIcon(this.L!, v),
        title: v.plateNumber,
      });

      marker.bindPopup(`
        <strong style="font-family:monospace;font-size:14px">${v.plateNumber}</strong><br/>
        ${v.make} ${v.model} (${v.year})<br/>
        <span style="color:${this.getVehicleColor(v.status)};font-weight:600">${v.status}</span>
      `, { maxWidth: 200 });

      marker.on('click', () => this.selectedVehicle.set(v));
      marker.addTo(this.map!);
      this.vehicleMarkers.set(v.id, marker);
    });
  }

  private updateGeofencePolygons(geofences: Geofence[], show: boolean): void {
    if (!this.map || !this.L) return;

    this.geofencePolygons.forEach(p => p.remove());
    this.geofencePolygons.clear();

    if (!show) return;

    geofences.filter(g => g.isActive).forEach(g => {
      const coords = g.coordinates.map(c => [c.latitude, c.longitude] as [number, number]);
      const color = g.type === 'Inclusion' ? '#2196F3' : '#F44336';

      const polygon = this.L!.polygon(coords, {
        color,
        fillColor: color,
        fillOpacity: 0.15,
        weight: 2,
        dashArray: g.type === 'Exclusion' ? '8,4' : undefined,
      });

      polygon.bindPopup(`
        <strong>${g.name}</strong><br/>
        Tipo: <span style="color:${color};font-weight:600">${g.type === 'Inclusion' ? 'Zona permitida' : 'Zona prohibida'}</span>
      `);

      polygon.addTo(this.map!);
      this.geofencePolygons.set(g.id, polygon);
    });
  }

  private updateViolationMarkers(violations: ViolationEvent[], show: boolean): void {
    if (!this.map || !this.L) return;

    this.violationMarkers.forEach(m => m.remove());
    this.violationMarkers = [];

    if (!show) return;

    violations.slice(0, 20).forEach(v => {
      const circle = this.L!.circleMarker([v.latitude, v.longitude], {
        radius: 10,
        fillColor: '#F44336',
        color: '#D32F2F',
        weight: 2,
        fillOpacity: 0.7,
      });

      circle.bindPopup(`
        <strong style="color:#D32F2F">${VIOLATION_TYPE_LABELS[v.violationType]}</strong><br/>
        ${v.geofenceName}<br/>
        ${v.actualSpeedKmh ? `Velocidad: ${v.actualSpeedKmh} km/h<br/>` : ''}
        <small>${new Date(v.occurredAt).toLocaleString('es-CO')}</small>
      `);

      circle.addTo(this.map!);
      this.violationMarkers.push(circle);
    });
  }

  fitBounds(): void {
    if (!this.map || !this.L) return;

    const coords: [number, number][] = [];
    this.vehicleMarkers.forEach((_, id) => {
      const v = this.vehiclesWithCoords().find(vh => vh.id === id);
      if (v?.currentLatitude != null && v.currentLongitude != null) {
        coords.push([v.currentLatitude, v.currentLongitude]);
      }
    });
    this.geofencePolygons.forEach(p => {
      const bounds = p.getBounds();
      coords.push([bounds.getCenter().lat, bounds.getCenter().lng]);
    });

    if (coords.length > 0) {
      this.map.fitBounds(this.L.latLngBounds(coords), { padding: [40, 40] });
    }
  }

  async refresh(): Promise<void> {
    await Promise.all([
      this.vehiclesStore.loadAll(),
      this.geofencesStore.loadAll(),
    ]);
  }
}
