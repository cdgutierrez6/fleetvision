import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ViolationEvent, ViolationType, VIOLATION_TYPE_LABELS, VIOLATION_TYPE_ICONS } from '@fleetvision/shared/models';
import { ViolationsStore, VehiclesStore } from '@fleetvision/shared/data-access';
import { ViolationAlertComponent, SkeletonLoaderComponent } from '@fleetvision/shared/ui';

interface HistoricalState {
  items: ViolationEvent[];
  isLoading: boolean;
  error: string | null;
}

@Component({
  selector: 'fv-feed',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatSelectModule,
    MatFormFieldModule,
    MatProgressSpinnerModule,
    MatDividerModule,
    ViolationAlertComponent,
    SkeletonLoaderComponent,
  ],
  styles: [`
    :host { display: block; height: 100%; background: var(--fv-bg, #F5F7FA); }

    .feed-layout {
      display: grid;
      grid-template-columns: 280px 1fr;
      height: 100%;
    }
    @media (max-width: 900px) { .feed-layout { grid-template-columns: 1fr; } }

    /* Sidebar filters */
    .filter-panel {
      border-right: 1px solid var(--fv-border, #E0E4EA);
      background: #fff;
      padding: 20px;
      overflow-y: auto;
    }
    .filter-title {
      font-size: 13px;
      font-weight: 700;
      color: var(--fv-primary, #1E3A5F);
      text-transform: uppercase;
      letter-spacing: .5px;
      margin: 0 0 16px;
    }
    .filter-section { margin-bottom: 24px; }
    .filter-label { font-size: 12px; color: var(--fv-text-muted, #6B7280); margin-bottom: 8px; }
    .filter-chips { display: flex; flex-wrap: wrap; gap: 6px; }
    .type-chip {
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 4px 10px;
      border-radius: 20px;
      border: 1px solid var(--fv-border, #E0E4EA);
      cursor: pointer;
      font-size: 12px;
      transition: all .15s;
      background: #fff;
      color: var(--fv-text, #1A2332);
    }
    .type-chip.active {
      background: var(--fv-primary, #1E3A5F);
      color: #fff;
      border-color: var(--fv-primary, #1E3A5F);
    }
    .date-inputs { display: flex; flex-direction: column; gap: 8px; }

    /* Main feed */
    .feed-main { display: flex; flex-direction: column; overflow: hidden; }

    .feed-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 20px 24px;
      background: #fff;
      border-bottom: 1px solid var(--fv-border, #E0E4EA);
    }
    .feed-title { font-size: 18px; font-weight: 700; color: var(--fv-primary, #1E3A5F); margin: 0; }
    .connection-indicator {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 12px;
      color: var(--fv-text-muted, #6B7280);
    }
    .conn-dot {
      width: 8px; height: 8px; border-radius: 50%;
    }
    .conn-dot.connected { background: #4CAF50; animation: pulse-green 2s infinite; }
    .conn-dot.disconnected { background: #9E9E9E; }
    @keyframes pulse-green {
      0%, 100% { box-shadow: 0 0 0 0 rgba(76,175,80,.4); }
      50% { box-shadow: 0 0 0 6px rgba(76,175,80,0); }
    }

    /* Tabs: live / history */
    .tabs {
      display: flex;
      gap: 0;
      border-bottom: 1px solid var(--fv-border, #E0E4EA);
      background: #fff;
      padding: 0 24px;
    }
    .tab {
      padding: 12px 20px;
      cursor: pointer;
      font-size: 13px;
      font-weight: 500;
      color: var(--fv-text-muted, #6B7280);
      border-bottom: 2px solid transparent;
      transition: all .15s;
    }
    .tab.active { color: var(--fv-primary, #1E3A5F); border-bottom-color: var(--fv-accent, #00BFA5); }

    .feed-content { flex: 1; overflow-y: auto; padding: 16px 24px; }

    /* Stats bar */
    .stats-bar {
      display: flex;
      gap: 16px;
      margin-bottom: 16px;
      flex-wrap: wrap;
    }
    .stat-chip {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 6px 12px;
      border-radius: 8px;
      font-size: 12px;
      font-weight: 500;
      background: #fff;
      border: 1px solid var(--fv-border, #E0E4EA);
    }
    .stat-value { font-weight: 700; color: var(--fv-primary, #1E3A5F); }

    /* Alert list */
    .alerts-list { display: flex; flex-direction: column; gap: 10px; }
    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 12px;
      padding: 60px 20px;
      color: var(--fv-text-muted, #6B7280);
    }
    .empty-state mat-icon { font-size: 56px; width: 56px; height: 56px; opacity: .25; }
    .empty-state h3 { font-size: 15px; font-weight: 600; margin: 0; color: var(--fv-text, #1A2332); }
    .empty-state p { font-size: 13px; margin: 0; text-align: center; }

    .load-more {
      display: flex;
      justify-content: center;
      padding: 16px 0;
    }
  `],
  template: `
    <div class="feed-layout">

      <!-- Filter sidebar -->
      <div class="filter-panel">
        <h2 class="filter-title">Filtros</h2>

        <div class="filter-section">
          <div class="filter-label">Tipo de violación</div>
          <div class="filter-chips">
            <span
              class="type-chip"
              [class.active]="!activeTypeFilter()"
              (click)="activeTypeFilter.set(null)">
              Todos
            </span>
            @for (type of violationTypes; track type) {
              <span
                class="type-chip"
                [class.active]="activeTypeFilter() === type"
                (click)="activeTypeFilter.set(type)">
                {{ typeIcons[type] }} {{ typeLabels[type] }}
              </span>
            }
          </div>
        </div>

        <div class="filter-section">
          <div class="filter-label">Vehículo</div>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" style="width:100%">
            <mat-select [(ngModel)]="vehicleFilter" placeholder="Todos los vehículos">
              <mat-option [value]="null">Todos</mat-option>
              @for (v of vehiclesStore.entities(); track v.id) {
                <mat-option [value]="v.id">{{ v.plateNumber }} — {{ v.make }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>

        <div class="filter-section">
          <div class="filter-label">Fecha (historial)</div>
          <div class="date-inputs">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>Desde</mat-label>
              <input matInput type="datetime-local" [(ngModel)]="dateFrom" />
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>Hasta</mat-label>
              <input matInput type="datetime-local" [(ngModel)]="dateTo" />
            </mat-form-field>
            <button mat-stroked-button color="primary" (click)="loadHistory()">
              <mat-icon>search</mat-icon>
              Buscar historial
            </button>
          </div>
        </div>

        <button mat-stroked-button style="width:100%" (click)="clearFilters()">
          <mat-icon>filter_alt_off</mat-icon>
          Limpiar filtros
        </button>
      </div>

      <!-- Main feed -->
      <div class="feed-main">

        <div class="feed-header">
          <h1 class="feed-title">Alertas de Violaciones</h1>
          <div class="connection-indicator">
            <span class="conn-dot" [class.connected]="violationsStore.isConnected()" [class.disconnected]="!violationsStore.isConnected()"></span>
            {{ violationsStore.isConnected() ? 'Tiempo real activo' : 'Sin conexión' }}
          </div>
        </div>

        <div class="tabs">
          <span class="tab" [class.active]="activeTab() === 'live'" (click)="activeTab.set('live')">
            En vivo ({{ violationsStore.violations().length }})
          </span>
          <span class="tab" [class.active]="activeTab() === 'history'" (click)="activeTab.set('history')">
            Historial
          </span>
        </div>

        <div class="feed-content">

          <!-- Stats -->
          <div class="stats-bar">
            <div class="stat-chip">
              <mat-icon style="font-size:16px;width:16px;height:16px;color:#F44336">warning</mat-icon>
              <span>Hoy:</span>
              <span class="stat-value">{{ violationsStore.violationsToday() }}</span>
            </div>
            <div class="stat-chip">
              <mat-icon style="font-size:16px;width:16px;height:16px;color:#1E3A5F">history</mat-icon>
              <span>Últimas 24h:</span>
              <span class="stat-value">{{ filteredLive().length }}</span>
            </div>
          </div>

          @if (activeTab() === 'live') {

            @if (filteredLive().length === 0) {
              <div class="empty-state">
                <mat-icon>check_circle_outline</mat-icon>
                <h3>Sin alertas activas</h3>
                <p>Las violaciones de geofence aparecerán aquí en tiempo real cuando ocurran.</p>
              </div>
            } @else {
              <div class="alerts-list">
                @for (v of filteredLive(); track v.id) {
                  <fv-violation-alert [violation]="v" />
                }
              </div>
            }

          } @else {

            @if (historical().isLoading) {
              <div class="alerts-list">
                @for (_ of [1,2,3,4,5]; track $index) {
                  <fv-skeleton-loader [height]="80" [radius]="8" />
                }
              </div>
            } @else if (historical().error) {
              <div class="empty-state">
                <mat-icon color="warn">error_outline</mat-icon>
                <h3>Error al cargar el historial</h3>
                <p>{{ historical().error }}</p>
                <button mat-stroked-button (click)="loadHistory()">Reintentar</button>
              </div>
            } @else if (historical().items.length === 0) {
              <div class="empty-state">
                <mat-icon>history</mat-icon>
                <h3>Sin resultados</h3>
                <p>Ajusta los filtros de fecha o selecciona un rango diferente.</p>
              </div>
            } @else {
              <div class="alerts-list">
                @for (v of filteredHistorical(); track v.id) {
                  <fv-violation-alert [violation]="v" />
                }
              </div>
              @if (filteredHistorical().length < historical().items.length) {
                <div class="load-more">
                  <span style="font-size:12px;color:#9E9E9E">Mostrando {{ filteredHistorical().length }} de {{ historical().items.length }}</span>
                </div>
              }
            }

          }
        </div>
      </div>
    </div>
  `,
})
export class FeedComponent implements OnInit {
  readonly violationsStore = inject(ViolationsStore);
  readonly vehiclesStore = inject(VehiclesStore);
  readonly http = inject(HttpClient);

  activeTab = signal<'live' | 'history'>('live');
  activeTypeFilter = signal<ViolationType | null>(null);
  vehicleFilter: string | null = null;

  dateFrom = '';
  dateTo = '';

  historical = signal<HistoricalState>({ items: [], isLoading: false, error: null });

  readonly violationTypes: ViolationType[] = [
    'SpeedExceeded', 'EnteredForbiddenZone', 'ExitedAllowedZone', 'OutsideSchedule',
  ];
  readonly typeLabels = VIOLATION_TYPE_LABELS;
  readonly typeIcons = VIOLATION_TYPE_ICONS;

  filteredLive = computed(() => {
    let items = this.violationsStore.violations();
    const type = this.activeTypeFilter();
    const veh = this.vehicleFilter;
    if (type) items = items.filter(v => v.violationType === type);
    if (veh)  items = items.filter(v => v.vehicleId === veh);
    return items;
  });

  filteredHistorical = computed(() => {
    let items = this.historical().items;
    const type = this.activeTypeFilter();
    const veh = this.vehicleFilter;
    if (type) items = items.filter(v => v.violationType === type);
    if (veh)  items = items.filter(v => v.vehicleId === veh);
    return items.slice(0, 100);
  });

  ngOnInit(): void {
    this.violationsStore.markAllRead();
    if (this.vehiclesStore.totalCount() === 0) {
      this.vehiclesStore.loadAll();
    }
  }

  async loadHistory(): Promise<void> {
    this.historical.set({ items: [], isLoading: true, error: null });
    try {
      const params: Record<string, string> = {};
      if (this.dateFrom) params['from'] = new Date(this.dateFrom).toISOString();
      if (this.dateTo)   params['to']   = new Date(this.dateTo).toISOString();
      if (this.vehicleFilter) params['vehicleId'] = this.vehicleFilter;

      const query = new URLSearchParams(params).toString();
      const items = await firstValueFrom(
        this.http.get<ViolationEvent[]>(`/api/geofencing/violations?${query}`)
      );
      this.historical.set({ items: items ?? [], isLoading: false, error: null });
    } catch {
      this.historical.set({ items: [], isLoading: false, error: 'No se pudo cargar el historial.' });
    }
  }

  clearFilters(): void {
    this.activeTypeFilter.set(null);
    this.vehicleFilter = null;
    this.dateFrom = '';
    this.dateTo = '';
  }
}
