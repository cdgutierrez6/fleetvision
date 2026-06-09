import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatDividerModule } from '@angular/material/divider';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ViolationsStore } from '@fleetvision/shared/data-access';
import { ReportingStore, DateRange } from '@fleetvision/shared/data-access';
import {
  ViolationType,
  VIOLATION_TYPE_LABELS,
  VIOLATION_TYPE_ICONS,
} from '@fleetvision/shared/models';
import { KpiCardComponent, SkeletonLoaderComponent } from '@fleetvision/shared/ui';

interface ViolationStatBar {
  type:  string;
  label: string;
  count: number;
  pct:   number;
  color: string;
}

const VIOLATION_COLORS: Record<string, string> = {
  SpeedExceeded:        '#F44336',
  EnteredForbiddenZone: '#FF5722',
  ExitedAllowedZone:    '#FF9800',
  OutsideSchedule:      '#9C27B0',
};

@Component({
  selector: 'fv-reports',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatButtonModule, MatIconModule,
    MatProgressBarModule, MatDividerModule, MatTooltipModule,
    KpiCardComponent, SkeletonLoaderComponent,
  ],
  styles: [`
    :host { display: block; background: var(--fv-bg, #F5F7FA); min-height: 100%; }
    .page { max-width: 1200px; margin: 0 auto; padding: 24px 28px; }

    .page-header {
      display: flex; align-items: center; justify-content: space-between;
      margin-bottom: 24px; flex-wrap: wrap; gap: 12px;
    }
    .page-title   { font-size: 22px; font-weight: 700; color: var(--fv-primary, #1E3A5F); margin: 0; }
    .page-subtitle { font-size: 13px; color: var(--fv-text-muted, #6B7280); margin: 2px 0 0; }
    .header-controls { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }

    .kpi-grid {
      display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; margin-bottom: 24px;
    }
    @media (max-width: 1100px) { .kpi-grid { grid-template-columns: repeat(2, 1fr); } }

    .cards-row {
      display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 24px;
    }
    @media (max-width: 900px) { .cards-row { grid-template-columns: 1fr; } }

    .card {
      background: #fff; border-radius: 12px;
      border: 1px solid var(--fv-border, #E0E4EA); padding: 20px;
    }
    .card-title {
      font-size: 14px; font-weight: 700; color: var(--fv-primary, #1E3A5F);
      margin: 0 0 16px; display: flex; align-items: center; gap: 8px;
    }
    .card-title mat-icon { font-size: 18px; width: 18px; height: 18px; color: var(--fv-accent, #00BFA5); }

    .bar-list { display: flex; flex-direction: column; gap: 14px; }
    .bar-row { display: flex; flex-direction: column; gap: 5px; }
    .bar-header { display: flex; justify-content: space-between; align-items: center; font-size: 13px; }
    .bar-label { color: var(--fv-text, #1A2332); display: flex; align-items: center; gap: 6px; }
    .bar-label mat-icon { font-size: 15px; width: 15px; height: 15px; }
    .bar-value { font-weight: 600; color: var(--fv-primary, #1E3A5F); }
    .bar-track { height: 8px; border-radius: 4px; background: #F0F4F8; overflow: hidden; }
    .bar-fill  { height: 100%; border-radius: 4px; transition: width .4s ease; }

    .status-dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%; margin-right: 6px; }
    .summary-table { width: 100%; border-collapse: collapse; font-size: 13px; }
    .summary-table th {
      text-align: left; padding: 8px 12px 8px 0;
      border-bottom: 2px solid var(--fv-border, #E0E4EA);
      font-size: 11px; font-weight: 700;
      color: var(--fv-text-muted, #6B7280); text-transform: uppercase; letter-spacing: .4px;
    }
    .summary-table td { padding: 10px 12px 10px 0; border-bottom: 1px solid #F3F4F6; }
    .summary-table tr:last-child td { border-bottom: none; }

    .range-chip {
      padding: 5px 12px; border-radius: 20px; font-size: 12px; font-weight: 500;
      border: 1px solid var(--fv-border, #E0E4EA); cursor: pointer;
      background: #fff; color: var(--fv-text, #1A2332); transition: all .15s;
    }
    .range-chip.active {
      background: var(--fv-primary, #1E3A5F); color: #fff; border-color: var(--fv-primary, #1E3A5F);
    }

    .empty {
      display: flex; flex-direction: column; align-items: center;
      justify-content: center; padding: 40px 16px;
      color: var(--fv-text-muted, #6B7280); gap: 8px;
    }
    .empty mat-icon { font-size: 40px; width: 40px; height: 40px; opacity: .2; }
    .empty span { font-size: 13px; }

    .error-banner {
      background: #FEF2F2; border: 1px solid #FECACA; border-radius: 8px;
      padding: 12px 16px; color: #B91C1C; font-size: 13px; margin-bottom: 16px;
      display: flex; align-items: center; gap: 8px;
    }
  `],
  template: `
    <div class="page">

      <!-- Header -->
      <div class="page-header">
        <div>
          <h1 class="page-title">Reportes & Analítica</h1>
          <p class="page-subtitle">Datos históricos de la flota</p>
        </div>
        <div class="header-controls">
          @for (r of ranges; track r.value) {
            <span
              class="range-chip"
              [class.active]="reportingStore.range() === r.value"
              (click)="changeRange(r.value)">
              {{ r.label }}
            </span>
          }
          <button mat-flat-button color="primary"
            [disabled]="reportingStore.isLoading()"
            (click)="reportingStore.loadAll()">
            <mat-icon>refresh</mat-icon>
            Actualizar
          </button>
          <button mat-stroked-button
            [disabled]="reportingStore.isExporting() || !reportingStore.hasData()"
            (click)="exportPdf()"
            matTooltip="Exportar reporte en PDF">
            <mat-icon>picture_as_pdf</mat-icon>
            {{ reportingStore.isExporting() ? 'Generando…' : 'PDF' }}
          </button>
        </div>
      </div>

      <!-- Error banner -->
      @if (reportingStore.error()) {
        <div class="error-banner">
          <mat-icon>error_outline</mat-icon>
          {{ reportingStore.error() }}
        </div>
      }

      <!-- KPIs históricos -->
      @if (reportingStore.isLoading()) {
        <div class="kpi-grid">
          @for (_ of [1,2,3,4]; track $index) {
            <fv-skeleton-loader [height]="88" [radius]="12" />
          }
        </div>
      } @else {
        <div class="kpi-grid">
          <fv-kpi-card
            icon="directions_car"
            [value]="reportingStore.activeVehicles()"
            label="Vehículos activos"
            color="#1E3A5F" />
          <fv-kpi-card
            icon="route"
            [value]="reportingStore.totalDistanceKm()"
            label="Distancia total (km)"
            color="#00BFA5" />
          <fv-kpi-card
            icon="speed"
            [value]="reportingStore.avgSpeedKmh()"
            label="Vel. promedio (km/h)"
            color="#1E88E5" />
          <fv-kpi-card
            icon="warning"
            [value]="reportingStore.totalViolations()"
            label="Alertas en período"
            color="#FF9800" />
        </div>
      }

      <!-- Charts row -->
      <div class="cards-row">

        <!-- Violations by type (histórico) -->
        <div class="card">
          <p class="card-title">
            <mat-icon>bar_chart</mat-icon>
            Alertas por tipo ({{ rangeLabelMap[reportingStore.range()] }})
          </p>
          @if (reportingStore.isLoading()) {
            @for (_ of [1,2,3,4]; track $index) {
              <div style="margin-bottom:14px"><fv-skeleton-loader [height]="32" [radius]="4" /></div>
            }
          } @else if (historicalViolationStats().length === 0) {
            <div class="empty">
              <mat-icon>check_circle</mat-icon>
              <span>Sin alertas en el período seleccionado</span>
            </div>
          } @else {
            <div class="bar-list">
              @for (stat of historicalViolationStats(); track stat.type) {
                <div class="bar-row">
                  <div class="bar-header">
                    <span class="bar-label">
                      <mat-icon [style.color]="stat.color">{{ violationIcons[stat.type] ?? 'warning' }}</mat-icon>
                      {{ stat.label }}
                    </span>
                    <span class="bar-value">{{ stat.count }}</span>
                  </div>
                  <div class="bar-track">
                    <div class="bar-fill" [style.width]="stat.pct + '%'" [style.background]="stat.color"></div>
                  </div>
                </div>
              }
            </div>
          }
        </div>

        <!-- Fleet status distribution -->
        <div class="card">
          <p class="card-title">
            <mat-icon>donut_small</mat-icon>
            Distribución de flota
          </p>
          @if (reportingStore.isLoading()) {
            @for (_ of [1,2,3]; track $index) {
              <div style="margin-bottom:14px"><fv-skeleton-loader [height]="36" [radius]="4" /></div>
            }
          } @else if (reportingStore.fleetTotal() === 0) {
            <div class="empty">
              <mat-icon>directions_car</mat-icon>
              <span>Sin datos de flota disponibles</span>
            </div>
          } @else {
            <div class="bar-list">
              @for (stat of fleetStatusStats(); track stat.label) {
                <div class="bar-row">
                  <div class="bar-header">
                    <span class="bar-label">
                      <span class="status-dot" [style.background]="stat.color"></span>
                      {{ stat.label }}
                    </span>
                    <span class="bar-value">
                      {{ stat.count }}
                      <span style="font-weight:400;color:#9CA3AF">({{ stat.pct }}%)</span>
                    </span>
                  </div>
                  <div class="bar-track">
                    <div class="bar-fill" [style.width]="stat.pct + '%'" [style.background]="stat.color"></div>
                  </div>
                </div>
              }
            </div>

            <mat-divider style="margin: 16px 0" />
            <table class="summary-table">
              <thead>
                <tr>
                  <th>Estado</th><th>Cantidad</th><th>Porcentaje</th>
                </tr>
              </thead>
              <tbody>
                @for (stat of fleetStatusStats(); track stat.label) {
                  <tr>
                    <td><span class="status-dot" [style.background]="stat.color"></span>{{ stat.label }}</td>
                    <td style="font-weight:600">{{ stat.count }}</td>
                    <td>{{ stat.pct }}%</td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>

      </div>

      <!-- Live session violations -->
      <div class="card">
        <p class="card-title">
          <mat-icon>sensors</mat-icon>
          Alertas en sesión (tiempo real)
        </p>
        @if (violationsStore.violations().length === 0) {
          <div class="empty">
            <mat-icon>notifications_off</mat-icon>
            <span>No hay alertas en la sesión actual</span>
          </div>
        } @else {
          <div style="display:flex;flex-direction:column;gap:0">
            @for (v of recentLiveViolations(); track v.id) {
              <div style="display:flex;align-items:flex-start;gap:12px;padding:12px 0;border-bottom:1px solid #F3F4F6">
                <div style="width:34px;height:34px;border-radius:50%;display:flex;align-items:center;justify-content:center;flex-shrink:0"
                     [style.background]="violationColor(v.violationType)">
                  <mat-icon style="font-size:17px;width:17px;height:17px;color:#fff">
                    {{ violationIcons[v.violationType] ?? 'warning' }}
                  </mat-icon>
                </div>
                <div style="flex:1;min-width:0">
                  <div style="font-size:13px;font-weight:600;color:var(--fv-primary,#1E3A5F)">
                    {{ violationLabels[v.violationType] ?? v.violationType }}
                  </div>
                  <div style="font-size:11px;color:var(--fv-text-muted,#6B7280);margin-top:1px">
                    Vehículo {{ v.vehicleId.slice(0, 8) }}…
                    @if (v.geofenceName) { · {{ v.geofenceName }} }
                    @if (v.actualSpeedKmh) { · {{ v.actualSpeedKmh | number:'1.0-0' }} km/h }
                  </div>
                </div>
                <span style="font-size:11px;color:var(--fv-text-muted,#6B7280);white-space:nowrap;flex-shrink:0">
                  {{ v.occurredAt | date:'HH:mm' }}
                </span>
              </div>
            }
          </div>
        }
      </div>

    </div>
  `,
})
export class ReportsComponent implements OnInit {
  readonly reportingStore  = inject(ReportingStore);
  readonly violationsStore = inject(ViolationsStore);

  readonly ranges: { value: DateRange; label: string }[] = [
    { value: '7d',  label: '7 días' },
    { value: '30d', label: '30 días' },
    { value: '90d', label: '90 días' },
  ];

  readonly rangeLabelMap: Record<DateRange, string> = {
    '7d':  '7 días',
    '30d': '30 días',
    '90d': '90 días',
  };

  readonly violationLabels = VIOLATION_TYPE_LABELS;
  readonly violationIcons  = VIOLATION_TYPE_ICONS;

  historicalViolationStats = computed<ViolationStatBar[]>(() => {
    const summary = this.reportingStore.violationsSummary();
    if (!summary || summary.totalViolations === 0) return [];
    return summary.byType.map(vt => ({
      type:  vt.violationType,
      label: VIOLATION_TYPE_LABELS[vt.violationType as ViolationType] ?? vt.violationType,
      count: vt.count,
      pct:   vt.percentage,
      color: VIOLATION_COLORS[vt.violationType] ?? '#9E9E9E',
    }));
  });

  fleetStatusStats = computed(() => {
    const s = this.reportingStore.fleetStatus();
    if (!s || s.total === 0) return [];
    return [
      { label: 'Activos',          count: s.active,      pct: Math.round((s.active / s.total) * 100),      color: '#4CAF50' },
      { label: 'En mantenimiento', count: s.maintenance, pct: Math.round((s.maintenance / s.total) * 100), color: '#9C27B0' },
      { label: 'Inactivos',        count: s.inactive,    pct: Math.round((s.inactive / s.total) * 100),    color: '#9E9E9E' },
    ].filter(s => s.count > 0);
  });

  recentLiveViolations = computed(() =>
    this.violationsStore.violations().slice(0, 10)
  );

  ngOnInit(): void {
    this.reportingStore.loadAll();
  }

  changeRange(range: DateRange): void {
    this.reportingStore.setRange(range);
    this.reportingStore.loadAll();
  }

  exportPdf(): void {
    this.reportingStore.exportPdf('FleetVision');
  }

  violationColor(type: string): string {
    return VIOLATION_COLORS[type] ?? '#9E9E9E';
  }
}
