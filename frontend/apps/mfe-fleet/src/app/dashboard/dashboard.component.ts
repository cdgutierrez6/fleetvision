import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatDividerModule } from '@angular/material/divider';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FormsModule } from '@angular/forms';
import { Vehicle } from '@fleetvision/shared/models';
import {
  VehiclesStore,
  ViolationsStore,
} from '@fleetvision/shared/data-access';
import {
  KpiCardComponent,
  StatusBadgeComponent,
  SkeletonLoaderComponent,
  ViolationAlertComponent,
} from '@fleetvision/shared/ui';

@Component({
  selector: 'fv-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    MatSidenavModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatChipsModule,
    MatProgressBarModule,
    MatDividerModule,
    MatTooltipModule,
    KpiCardComponent,
    StatusBadgeComponent,
    SkeletonLoaderComponent,
    ViolationAlertComponent,
  ],
  styles: [`
    :host { display: block; height: 100%; }

    .dashboard-layout {
      display: flex;
      flex-direction: column;
      height: 100%;
      background: var(--fv-bg, #F5F7FA);
      padding: 24px;
      gap: 24px;
      overflow-y: auto;
    }

    .page-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .page-title {
      font-size: 22px;
      font-weight: 700;
      color: var(--fv-primary, #1E3A5F);
      margin: 0;
    }
    .page-subtitle {
      font-size: 13px;
      color: var(--fv-text-muted, #6B7280);
      margin: 2px 0 0;
    }

    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 16px;
    }
    @media (max-width: 1100px) { .kpi-grid { grid-template-columns: repeat(2, 1fr); } }
    @media (max-width: 600px)  { .kpi-grid { grid-template-columns: 1fr; } }

    .content-grid {
      display: grid;
      grid-template-columns: 1fr 360px;
      gap: 20px;
      min-height: 0;
    }
    @media (max-width: 1100px) { .content-grid { grid-template-columns: 1fr; } }

    .card {
      background: #fff;
      border-radius: 12px;
      border: 1px solid var(--fv-border, #E0E4EA);
      overflow: hidden;
    }
    .card-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 16px 20px;
      border-bottom: 1px solid var(--fv-border, #E0E4EA);
    }
    .card-title {
      font-size: 15px;
      font-weight: 600;
      color: var(--fv-primary, #1E3A5F);
      margin: 0;
    }
    .card-count {
      font-size: 12px;
      color: var(--fv-text-muted, #6B7280);
      background: #F3F4F6;
      padding: 2px 8px;
      border-radius: 20px;
    }

    .search-bar {
      padding: 12px 16px;
      border-bottom: 1px solid var(--fv-border, #E0E4EA);
    }
    .search-bar mat-form-field { width: 100%; }

    .vehicle-table {
      width: 100%;
    }
    .mat-mdc-row { cursor: pointer; transition: background .15s; }
    .mat-mdc-row:hover { background: #F0F4FF; }
    .mat-mdc-row.selected { background: #EEF2FF; }

    .plate-cell {
      font-weight: 600;
      color: var(--fv-primary, #1E3A5F);
      font-family: 'Courier New', monospace;
      font-size: 13px;
      letter-spacing: .5px;
    }
    .vehicle-make { font-size: 13px; color: var(--fv-text, #1A2332); }
    .vehicle-year { font-size: 12px; color: var(--fv-text-muted, #6B7280); }
    .last-seen { font-size: 12px; color: var(--fv-text-muted, #6B7280); }
    .gps-dot {
      display: inline-block;
      width: 8px; height: 8px;
      border-radius: 50%;
      margin-right: 4px;
    }
    .gps-dot.has-gps { background: #4CAF50; }
    .gps-dot.no-gps { background: #9E9E9E; }

    /* Drawer */
    .detail-drawer {
      width: 400px;
      border-left: 1px solid var(--fv-border, #E0E4EA);
      background: #fff;
      overflow-y: auto;
    }
    .drawer-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 20px;
      background: var(--fv-primary, #1E3A5F);
      color: #fff;
    }
    .drawer-plate {
      font-size: 18px;
      font-weight: 700;
      font-family: 'Courier New', monospace;
      letter-spacing: 1px;
    }
    .drawer-make { font-size: 13px; opacity: .8; }
    .drawer-body { padding: 20px; }

    .detail-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 10px 0;
      border-bottom: 1px solid #F3F4F6;
    }
    .detail-row:last-child { border-bottom: none; }
    .detail-label { font-size: 12px; color: var(--fv-text-muted, #6B7280); text-transform: uppercase; letter-spacing: .5px; }
    .detail-value { font-size: 14px; font-weight: 500; color: var(--fv-text, #1A2332); }

    .map-preview {
      margin: 16px 0;
      background: #E8EFF8;
      border-radius: 8px;
      height: 160px;
      display: flex;
      align-items: center;
      justify-content: center;
      color: var(--fv-text-muted, #6B7280);
      font-size: 13px;
      flex-direction: column;
      gap: 8px;
    }
    .map-coords { font-size: 11px; font-family: monospace; }

    /* Recent violations in drawer */
    .drawer-section-title {
      font-size: 13px;
      font-weight: 600;
      color: var(--fv-primary, #1E3A5F);
      margin: 16px 0 8px;
      text-transform: uppercase;
      letter-spacing: .5px;
    }
    .no-violations {
      color: var(--fv-text-muted, #6B7280);
      font-size: 13px;
      text-align: center;
      padding: 20px 0;
    }

    /* Sidebar violations panel */
    .violations-panel { overflow-y: auto; max-height: 520px; }
    .violations-list { padding: 8px 16px; display: flex; flex-direction: column; gap: 8px; }
    .no-alerts {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      padding: 40px 20px;
      color: var(--fv-text-muted, #6B7280);
    }
    .no-alerts mat-icon { font-size: 40px; width: 40px; height: 40px; opacity: .4; }
  `],
  template: `
    <div class="dashboard-layout">

      <!-- Header -->
      <div class="page-header">
        <div>
          <h1 class="page-title">Panel de Flota</h1>
          <p class="page-subtitle">{{ vehiclesStore.totalCount() }} vehículos registrados en el tenant</p>
        </div>
        <button mat-flat-button color="primary" (click)="vehiclesStore.loadAll()">
          <mat-icon>refresh</mat-icon>
          Actualizar
        </button>
      </div>

      <!-- KPIs -->
      @if (vehiclesStore.isLoading()) {
        <div class="kpi-grid">
          @for (_ of [1,2,3,4]; track $index) {
            <fv-skeleton-loader [height]="96" [radius]="12" />
          }
        </div>
      } @else {
        <div class="kpi-grid">
          <fv-kpi-card
            icon="directions_car"
            [value]="vehiclesStore.totalCount()"
            label="Total vehículos"
            color="#1E3A5F" />
          <fv-kpi-card
            icon="check_circle"
            [value]="vehiclesStore.activeCount()"
            label="Activos"
            color="#4CAF50" />
          <fv-kpi-card
            icon="build"
            [value]="vehiclesStore.maintenanceCount()"
            label="En mantenimiento"
            color="#FF9800" />
          <fv-kpi-card
            icon="cancel"
            [value]="vehiclesStore.inactiveCount()"
            label="Inactivos"
            color="#9E9E9E" />
        </div>
      }

      <!-- Content grid: table + violations -->
      <div class="content-grid">

        <!-- Vehicle table card -->
        <div class="card">
          <div class="card-header">
            <span class="card-title">Vehículos</span>
            <span class="card-count">{{ filteredVehicles().length }} resultados</span>
          </div>

          <div class="search-bar">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>Buscar por placa, marca o modelo</mat-label>
              <mat-icon matPrefix>search</mat-icon>
              <input matInput [ngModel]="searchTerm()" (ngModelChange)="searchTerm.set($event)" placeholder="ABC-123" />
              @if (searchTerm()) {
                <button matSuffix mat-icon-button (click)="searchTerm.set('')">
                  <mat-icon>close</mat-icon>
                </button>
              }
            </mat-form-field>
          </div>

          @if (vehiclesStore.isLoading()) {
            <mat-progress-bar mode="indeterminate" color="accent" />
          }

          <mat-table [dataSource]="filteredVehicles()" class="vehicle-table">

            <ng-container matColumnDef="plate">
              <mat-header-cell *matHeaderCellDef>Placa</mat-header-cell>
              <mat-cell *matCellDef="let v">
                <span class="plate-cell">{{ v.plateNumber }}</span>
              </mat-cell>
            </ng-container>

            <ng-container matColumnDef="vehicle">
              <mat-header-cell *matHeaderCellDef>Vehículo</mat-header-cell>
              <mat-cell *matCellDef="let v">
                <div>
                  <div class="vehicle-make">{{ v.make }} {{ v.model }}</div>
                  <div class="vehicle-year">{{ v.year }}</div>
                </div>
              </mat-cell>
            </ng-container>

            <ng-container matColumnDef="status">
              <mat-header-cell *matHeaderCellDef>Estado</mat-header-cell>
              <mat-cell *matCellDef="let v">
                <fv-status-badge [status]="v.status" />
              </mat-cell>
            </ng-container>

            <ng-container matColumnDef="gps">
              <mat-header-cell *matHeaderCellDef>GPS</mat-header-cell>
              <mat-cell *matCellDef="let v">
                <span [class.has-gps]="hasGps(v)" [class.no-gps]="!hasGps(v)" class="gps-dot"></span>
                <span class="last-seen">
                  {{ hasGps(v) ? 'En línea' : 'Sin señal' }}
                </span>
              </mat-cell>
            </ng-container>

            <ng-container matColumnDef="lastSeen">
              <mat-header-cell *matHeaderCellDef>Última señal</mat-header-cell>
              <mat-cell *matCellDef="let v">
                <span class="last-seen">
                  {{ v.lastSeenAt ? (v.lastSeenAt | date:'dd/MM HH:mm') : '—' }}
                </span>
              </mat-cell>
            </ng-container>

            <mat-header-row *matHeaderRowDef="displayedColumns"></mat-header-row>
            <mat-row
              *matRowDef="let row; columns: displayedColumns"
              [class.selected]="selectedVehicle()?.id === row.id"
              (click)="selectVehicle(row)">
            </mat-row>

            <tr class="mat-row" *matNoDataRow>
              <td class="mat-cell" [attr.colspan]="displayedColumns.length" style="text-align:center;padding:32px;color:#9E9E9E;">
                @if (searchTerm) {
                  No se encontraron vehículos con "{{ searchTerm() }}"
                } @else {
                  No hay vehículos registrados
                }
              </td>
            </tr>
          </mat-table>
        </div>

        <!-- Recent violations panel -->
        <div class="card">
          <div class="card-header">
            <span class="card-title">Alertas recientes</span>
            @if (violationsStore.unreadCount() > 0) {
              <span class="card-count" style="background:#FEE2E2;color:#DC2626;">
                {{ violationsStore.unreadCount() }} nuevas
              </span>
            }
          </div>
          <div class="violations-panel">
            @if (violationsStore.violations().length === 0) {
              <div class="no-alerts">
                <mat-icon>check_circle_outline</mat-icon>
                <span>Sin alertas activas</span>
              </div>
            } @else {
              <div class="violations-list">
                @for (v of violationsStore.violations().slice(0, 10); track v.id) {
                  <fv-violation-alert [violation]="v" />
                }
              </div>
            }
          </div>
        </div>

      </div>
    </div>

    <!-- Vehicle detail drawer (mat-drawer emulation via conditional panel) -->
    @if (selectedVehicle()) {
      <div style="position:fixed;top:0;right:0;height:100%;z-index:200;display:flex;">
        <div class="detail-drawer">
          <div class="drawer-header">
            <div>
              <div class="drawer-plate">{{ selectedVehicle()!.plateNumber }}</div>
              <div class="drawer-make">{{ selectedVehicle()!.make }} {{ selectedVehicle()!.model }} · {{ selectedVehicle()!.year }}</div>
            </div>
            <button mat-icon-button style="color:#fff" (click)="selectedVehicle.set(null)">
              <mat-icon>close</mat-icon>
            </button>
          </div>

          <div class="drawer-body">
            <fv-status-badge [status]="selectedVehicle()!.status" />

            <!-- GPS preview -->
            @if (hasGps(selectedVehicle()!)) {
              <div class="map-preview">
                <mat-icon style="font-size:32px;width:32px;height:32px;color:#00BFA5;">location_on</mat-icon>
                <span>Posición GPS activa</span>
                <span class="map-coords">
                  {{ selectedVehicle()!.currentLatitude | number:'1.4-4' }},
                  {{ selectedVehicle()!.currentLongitude | number:'1.4-4' }}
                </span>
              </div>
            } @else {
              <div class="map-preview">
                <mat-icon style="font-size:32px;width:32px;height:32px;opacity:.3;">location_off</mat-icon>
                <span>Sin señal GPS</span>
              </div>
            }

            <!-- Details -->
            <div class="detail-row">
              <span class="detail-label">ID</span>
              <span class="detail-value" style="font-size:11px;font-family:monospace;">{{ selectedVehicle()!.id.slice(0,8) }}…</span>
            </div>
            <div class="detail-row">
              <span class="detail-label">Marca</span>
              <span class="detail-value">{{ selectedVehicle()!.make }}</span>
            </div>
            <div class="detail-row">
              <span class="detail-label">Modelo</span>
              <span class="detail-value">{{ selectedVehicle()!.model }}</span>
            </div>
            <div class="detail-row">
              <span class="detail-label">Año</span>
              <span class="detail-value">{{ selectedVehicle()!.year }}</span>
            </div>
            <div class="detail-row">
              <span class="detail-label">Última señal</span>
              <span class="detail-value">
                {{ selectedVehicle()!.lastSeenAt ? (selectedVehicle()!.lastSeenAt | date:'dd MMM yyyy HH:mm') : 'Sin datos' }}
              </span>
            </div>

            <!-- Vehicle violations -->
            <div class="drawer-section-title">Violaciones de este vehículo</div>
            @if (vehicleViolations().length === 0) {
              <div class="no-violations">Sin violaciones registradas</div>
            } @else {
              @for (v of vehicleViolations(); track v.id) {
                <fv-violation-alert [violation]="v" />
              }
            }
          </div>
        </div>
      </div>
    }
  `,
})
export class DashboardComponent implements OnInit {
  readonly vehiclesStore = inject(VehiclesStore);
  readonly violationsStore = inject(ViolationsStore);

  searchTerm = signal('');
  selectedVehicle = signal<Vehicle | null>(null);
  displayedColumns = ['plate', 'vehicle', 'status', 'gps', 'lastSeen'];

  filteredVehicles = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    const all = this.vehiclesStore.entities();
    if (!term) return all;
    return all.filter(v =>
      v.plateNumber.toLowerCase().includes(term) ||
      v.make.toLowerCase().includes(term) ||
      v.model.toLowerCase().includes(term)
    );
  });

  vehicleViolations = computed(() => {
    const v = this.selectedVehicle();
    if (!v) return [];
    return this.violationsStore.violations().filter(ev => ev.vehicleId === v.id);
  });

  constructor() {
    effect(() => {
      // Reactive: clears drawer when user types a new search
      if (this.searchTerm()) this.selectedVehicle.set(null);
    });
  }

  ngOnInit(): void {
    if (this.vehiclesStore.totalCount() === 0) {
      this.vehiclesStore.loadAll();
    }
  }

  selectVehicle(v: Vehicle): void {
    this.selectedVehicle.update(cur => cur?.id === v.id ? null : v);
    this.violationsStore.markAllRead();
  }

  hasGps(v: Vehicle): boolean {
    return v.currentLatitude != null && v.currentLongitude != null;
  }
}
