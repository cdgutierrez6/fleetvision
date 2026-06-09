import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDividerModule } from '@angular/material/divider';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import {
  TenantProfile,
  PlanTier,
  PLAN_TIER_COLORS,
  PLAN_TIER_LABELS,
} from '@fleetvision/shared/models';
import { TenantsStore } from '@fleetvision/shared/data-access';
import { KpiCardComponent, SkeletonLoaderComponent } from '@fleetvision/shared/ui';

type ActiveFilter = 'all' | 'active' | 'inactive';

@Component({
  selector: 'fv-admin',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatSlideToggleModule,
    MatSelectModule,
    MatFormFieldModule,
    MatProgressBarModule,
    MatTooltipModule,
    MatDividerModule,
    MatProgressSpinnerModule,
    KpiCardComponent,
    SkeletonLoaderComponent,
  ],
  styles: [`
    :host { display: block; height: 100%; background: var(--fv-bg, #F5F7FA); }

    .admin-layout {
      display: grid;
      grid-template-columns: 1fr 380px;
      height: 100%;
      overflow: hidden;
    }
    .admin-layout.no-drawer { grid-template-columns: 1fr; }

    /* ─── Main panel ──────────────────────────────────────────────── */
    .main-panel {
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }

    .page-header {
      padding: 24px 28px 0;
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
      padding: 20px 28px;
    }
    @media (max-width: 1200px) { .kpi-grid { grid-template-columns: repeat(2, 1fr); } }

    /* ─── Filter bar ──────────────────────────────────────────────── */
    .filter-bar {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 0 28px 16px;
      flex-wrap: wrap;
    }
    .filter-label {
      font-size: 12px;
      font-weight: 600;
      color: var(--fv-text-muted, #6B7280);
      text-transform: uppercase;
      letter-spacing: .5px;
      white-space: nowrap;
    }
    .filter-chip {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 5px 12px;
      border-radius: 20px;
      border: 1px solid var(--fv-border, #E0E4EA);
      font-size: 12px;
      font-weight: 500;
      cursor: pointer;
      transition: all .15s;
      background: #fff;
      color: var(--fv-text, #1A2332);
      user-select: none;
    }
    .filter-chip:hover { border-color: #B0BEC5; }
    .filter-chip.active {
      background: var(--fv-primary, #1E3A5F);
      color: #fff;
      border-color: var(--fv-primary, #1E3A5F);
    }
    .filter-chip .dot {
      width: 8px; height: 8px; border-radius: 50%;
    }
    .filter-separator { width: 1px; height: 20px; background: var(--fv-border, #E0E4EA); }

    /* ─── Table card ──────────────────────────────────────────────── */
    .table-card {
      flex: 1;
      margin: 0 28px 24px;
      background: #fff;
      border-radius: 12px;
      border: 1px solid var(--fv-border, #E0E4EA);
      overflow: hidden;
      display: flex;
      flex-direction: column;
    }
    .table-wrapper {
      overflow-y: auto;
      flex: 1;
    }

    .tenant-table { width: 100%; }

    .name-cell { font-weight: 600; color: var(--fv-primary, #1E3A5F); }
    .email-cell { font-size: 12px; color: var(--fv-text-muted, #6B7280); }

    .plan-badge {
      display: inline-flex;
      align-items: center;
      padding: 3px 10px;
      border-radius: 20px;
      font-size: 11px;
      font-weight: 700;
      letter-spacing: .3px;
      text-transform: uppercase;
    }

    .usage-bar-wrap { display: flex; flex-direction: column; gap: 3px; min-width: 80px; }
    .usage-label { font-size: 11px; color: var(--fv-text-muted, #6B7280); }
    .usage-track {
      height: 4px;
      border-radius: 2px;
      background: #E0E4EA;
      overflow: hidden;
    }
    .usage-fill { height: 100%; border-radius: 2px; transition: width .3s; }

    .date-cell { font-size: 12px; color: var(--fv-text-muted, #6B7280); }
    .mat-mdc-row { cursor: pointer; transition: background .15s; }
    .mat-mdc-row:hover { background: #F0F4FF; }
    .mat-mdc-row.selected { background: #EEF2FF; }

    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 60px 20px;
      color: var(--fv-text-muted, #6B7280);
      gap: 8px;
    }
    .empty-state mat-icon { font-size: 48px; width: 48px; height: 48px; opacity: .25; }

    .skeleton-list { padding: 12px 0; display: flex; flex-direction: column; gap: 4px; }

    /* ─── Detail drawer ───────────────────────────────────────────── */
    .drawer {
      background: #fff;
      border-left: 1px solid var(--fv-border, #E0E4EA);
      overflow-y: auto;
      display: flex;
      flex-direction: column;
    }

    .drawer-header {
      padding: 20px;
      background: var(--fv-primary, #1E3A5F);
      color: #fff;
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
    }
    .drawer-name { font-size: 18px; font-weight: 700; line-height: 1.2; }
    .drawer-email { font-size: 12px; opacity: .7; margin-top: 4px; }

    .drawer-body { padding: 20px; flex: 1; }

    .drawer-section {
      margin-bottom: 24px;
    }
    .drawer-section-title {
      font-size: 11px;
      font-weight: 700;
      color: var(--fv-text-muted, #6B7280);
      text-transform: uppercase;
      letter-spacing: .5px;
      margin: 0 0 12px;
    }

    .info-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 8px 0;
      border-bottom: 1px solid #F3F4F6;
      font-size: 13px;
    }
    .info-row:last-child { border-bottom: none; }
    .info-label { color: var(--fv-text-muted, #6B7280); }
    .info-value { font-weight: 500; }

    .limit-row {
      display: flex;
      flex-direction: column;
      gap: 4px;
      padding: 10px 0;
      border-bottom: 1px solid #F3F4F6;
    }
    .limit-row:last-child { border-bottom: none; }
    .limit-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 13px;
    }
    .limit-label { color: var(--fv-text-muted, #6B7280); }
    .limit-value { font-weight: 600; }
    .limit-track {
      height: 6px;
      border-radius: 3px;
      background: #E0E4EA;
      overflow: hidden;
    }
    .limit-fill { height: 100%; border-radius: 3px; transition: width .3s; }

    .plan-form {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .plan-form mat-form-field { width: 100%; }

    /* Confirmation panel */
    .confirm-panel {
      background: #FFF3E0;
      border: 1px solid #FF9800;
      border-radius: 8px;
      padding: 14px;
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    .confirm-message {
      font-size: 13px;
      color: #E65100;
      font-weight: 500;
    }
    .confirm-actions { display: flex; gap: 8px; }

    .action-buttons {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding-top: 8px;
    }

    .saving-overlay {
      display: flex;
      align-items: center;
      gap: 8px;
      color: var(--fv-text-muted, #6B7280);
      font-size: 13px;
      padding: 8px 0;
    }

    .status-active { color: #4CAF50; font-weight: 600; }
    .status-inactive { color: #9E9E9E; font-weight: 600; }
  `],
  template: `
    <div class="admin-layout" [class.no-drawer]="!selectedTenant()">

      <!-- ─── Main panel ──────────────────────────────────────────── -->
      <div class="main-panel">

        <!-- Header -->
        <div class="page-header">
          <div>
            <h1 class="page-title">Administración de Tenants</h1>
            <p class="page-subtitle">{{ tenantsStore.totalCount() }} organizaciones registradas</p>
          </div>
          <button mat-flat-button color="primary" (click)="tenantsStore.loadAll()">
            <mat-icon>refresh</mat-icon>
            Actualizar
          </button>
        </div>

        <!-- KPIs -->
        @if (tenantsStore.isLoading()) {
          <div class="kpi-grid">
            @for (_ of [1,2,3,4]; track $index) {
              <fv-skeleton-loader [height]="88" [radius]="12" />
            }
          </div>
        } @else {
          <div class="kpi-grid">
            <fv-kpi-card
              icon="business"
              [value]="tenantsStore.totalCount()"
              label="Total tenants"
              color="#1E3A5F" />
            <fv-kpi-card
              icon="check_circle"
              [value]="tenantsStore.activeCount()"
              label="Activos"
              color="#4CAF50" />
            <fv-kpi-card
              icon="workspace_premium"
              [value]="tenantsStore.byPlan()['Enterprise']"
              label="Enterprise"
              color="#9C27B0" />
            <fv-kpi-card
              icon="star"
              [value]="tenantsStore.byPlan()['Professional']"
              label="Professional"
              color="#2196F3" />
          </div>
        }

        <!-- Filter bar -->
        <div class="filter-bar">

          <span class="filter-label">Plan</span>
          <span class="filter-chip" [class.active]="planFilter() === null" (click)="planFilter.set(null)">
            Todos
          </span>
          @for (tier of planTiers; track tier) {
            <span
              class="filter-chip"
              [class.active]="planFilter() === tier"
              (click)="planFilter.set(tier)">
              <span class="dot" [style.background]="planColors[tier]"></span>
              {{ planLabels[tier] }}
            </span>
          }

          <span class="filter-separator"></span>

          <span class="filter-label">Estado</span>
          <span class="filter-chip" [class.active]="activeFilter() === 'all'" (click)="activeFilter.set('all')">
            Todos
          </span>
          <span class="filter-chip" [class.active]="activeFilter() === 'active'" (click)="activeFilter.set('active')">
            <span class="dot" style="background:#4CAF50"></span>
            Activos
          </span>
          <span class="filter-chip" [class.active]="activeFilter() === 'inactive'" (click)="activeFilter.set('inactive')">
            <span class="dot" style="background:#9E9E9E"></span>
            Inactivos
          </span>
        </div>

        <!-- Table -->
        <div class="table-card">

          @if (tenantsStore.isLoading()) {
            <div class="skeleton-list">
              @for (_ of [1,2,3,4,5,6]; track $index) {
                <fv-skeleton-loader [height]="52" [radius]="0" />
              }
            </div>
          } @else if (filteredTenants().length === 0) {
            <div class="empty-state">
              <mat-icon>business_center</mat-icon>
              <span>No hay tenants que coincidan con los filtros</span>
            </div>
          } @else {
            <div class="table-wrapper">
              @if (tenantsStore.isLoading()) {
                <mat-progress-bar mode="indeterminate" color="accent" />
              }
              <mat-table [dataSource]="filteredTenants()" class="tenant-table">

                <ng-container matColumnDef="name">
                  <mat-header-cell *matHeaderCellDef>Organización</mat-header-cell>
                  <mat-cell *matCellDef="let t">
                    <div>
                      <div class="name-cell">{{ t.name }}</div>
                      <div class="email-cell">{{ t.contactEmail }}</div>
                    </div>
                  </mat-cell>
                </ng-container>

                <ng-container matColumnDef="plan">
                  <mat-header-cell *matHeaderCellDef>Plan</mat-header-cell>
                  <mat-cell *matCellDef="let t">
                    <span
                      class="plan-badge"
                      [style.background]="planColors[t.planTier] + '22'"
                      [style.color]="planColors[t.planTier]">
                      {{ planLabels[t.planTier] }}
                    </span>
                  </mat-cell>
                </ng-container>

                <ng-container matColumnDef="status">
                  <mat-header-cell *matHeaderCellDef>Estado</mat-header-cell>
                  <mat-cell *matCellDef="let t">
                    @if (t.isActive) {
                      <span class="status-active">Activo</span>
                    } @else {
                      <span class="status-inactive">Inactivo</span>
                    }
                  </mat-cell>
                </ng-container>

                <ng-container matColumnDef="vehicles">
                  <mat-header-cell *matHeaderCellDef>Vehículos</mat-header-cell>
                  <mat-cell *matCellDef="let t">
                    <div class="usage-bar-wrap">
                      <span class="usage-label">{{ t.currentVehicles ?? 0 }} / {{ t.maxVehicles }}</span>
                      <div class="usage-track">
                        <div
                          class="usage-fill"
                          [style.width]="usagePct(t.currentVehicles, t.maxVehicles) + '%'"
                          [style.background]="usageColor(t.currentVehicles, t.maxVehicles)">
                        </div>
                      </div>
                    </div>
                  </mat-cell>
                </ng-container>

                <ng-container matColumnDef="users">
                  <mat-header-cell *matHeaderCellDef>Usuarios</mat-header-cell>
                  <mat-cell *matCellDef="let t">
                    <div class="usage-bar-wrap">
                      <span class="usage-label">{{ t.currentUsers ?? 0 }} / {{ t.maxUsers }}</span>
                      <div class="usage-track">
                        <div
                          class="usage-fill"
                          [style.width]="usagePct(t.currentUsers, t.maxUsers) + '%'"
                          [style.background]="usageColor(t.currentUsers, t.maxUsers)">
                        </div>
                      </div>
                    </div>
                  </mat-cell>
                </ng-container>

                <ng-container matColumnDef="createdAt">
                  <mat-header-cell *matHeaderCellDef>Creado</mat-header-cell>
                  <mat-cell *matCellDef="let t">
                    <span class="date-cell">{{ t.createdAt | date:'dd/MM/yyyy' }}</span>
                  </mat-cell>
                </ng-container>

                <mat-header-row *matHeaderRowDef="displayedColumns"></mat-header-row>
                <mat-row
                  *matRowDef="let row; columns: displayedColumns"
                  [class.selected]="selectedTenant()?.id === row.id"
                  (click)="select(row)">
                </mat-row>

              </mat-table>
            </div>
          }
        </div>
      </div>

      <!-- ─── Detail drawer ────────────────────────────────────────── -->
      @if (selectedTenant()) {
        <div class="drawer">
          <div class="drawer-header">
            <div style="flex:1;min-width:0">
              <div class="drawer-name">{{ selectedTenant()!.name }}</div>
              <div class="drawer-email">{{ selectedTenant()!.contactEmail }}</div>
            </div>
            <button mat-icon-button style="color:#fff;flex-shrink:0" (click)="selectedTenant.set(null)">
              <mat-icon>close</mat-icon>
            </button>
          </div>

          <div class="drawer-body">

            <!-- Estado general -->
            <div class="drawer-section">
              <p class="drawer-section-title">Información general</p>
              <div class="info-row">
                <span class="info-label">ID</span>
                <span class="info-value" style="font-size:11px;font-family:monospace">
                  {{ selectedTenant()!.id.slice(0, 8) }}…
                </span>
              </div>
              <div class="info-row">
                <span class="info-label">Estado</span>
                <span class="info-value">
                  @if (selectedTenant()!.isActive) {
                    <span class="status-active">● Activo</span>
                  } @else {
                    <span class="status-inactive">● Inactivo</span>
                  }
                </span>
              </div>
              <div class="info-row">
                <span class="info-label">Plan actual</span>
                <span
                  class="plan-badge"
                  [style.background]="planColors[selectedTenant()!.planTier] + '22'"
                  [style.color]="planColors[selectedTenant()!.planTier]">
                  {{ planLabels[selectedTenant()!.planTier] }}
                </span>
              </div>
              <div class="info-row">
                <span class="info-label">Creado</span>
                <span class="info-value">{{ selectedTenant()!.createdAt | date:'dd MMM yyyy' }}</span>
              </div>
            </div>

            <mat-divider />

            <!-- Uso de recursos -->
            <div class="drawer-section" style="margin-top:16px">
              <p class="drawer-section-title">Uso de recursos</p>
              <div class="limit-row">
                <div class="limit-header">
                  <span class="limit-label">Vehículos</span>
                  <span class="limit-value">
                    {{ selectedTenant()!.currentVehicles ?? 0 }} / {{ selectedTenant()!.maxVehicles }}
                  </span>
                </div>
                <div class="limit-track">
                  <div
                    class="limit-fill"
                    [style.width]="usagePct(selectedTenant()!.currentVehicles, selectedTenant()!.maxVehicles) + '%'"
                    [style.background]="usageColor(selectedTenant()!.currentVehicles, selectedTenant()!.maxVehicles)">
                  </div>
                </div>
              </div>
              <div class="limit-row">
                <div class="limit-header">
                  <span class="limit-label">Usuarios</span>
                  <span class="limit-value">
                    {{ selectedTenant()!.currentUsers ?? 0 }} / {{ selectedTenant()!.maxUsers }}
                  </span>
                </div>
                <div class="limit-track">
                  <div
                    class="limit-fill"
                    [style.width]="usagePct(selectedTenant()!.currentUsers, selectedTenant()!.maxUsers) + '%'"
                    [style.background]="usageColor(selectedTenant()!.currentUsers, selectedTenant()!.maxUsers)">
                  </div>
                </div>
              </div>
            </div>

            <mat-divider />

            <!-- Cambiar plan -->
            <div class="drawer-section" style="margin-top:16px">
              <p class="drawer-section-title">Cambiar plan</p>
              <div class="plan-form">
                <mat-form-field appearance="outline" subscriptSizing="dynamic">
                  <mat-label>Plan</mat-label>
                  <mat-select [(ngModel)]="pendingPlan">
                    @for (tier of planTiers; track tier) {
                      <mat-option [value]="tier">
                        {{ planLabels[tier] }}
                      </mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                @if (tenantsStore.isSaving()) {
                  <div class="saving-overlay">
                    <mat-spinner [diameter]="16" color="accent"></mat-spinner>
                    Guardando…
                  </div>
                } @else {
                  <button
                    mat-flat-button
                    color="primary"
                    [disabled]="pendingPlan === selectedTenant()!.planTier"
                    (click)="savePlan()">
                    <mat-icon>save</mat-icon>
                    Guardar cambio de plan
                  </button>
                }
              </div>
            </div>

            <mat-divider />

            <!-- Activar / Desactivar -->
            <div class="drawer-section" style="margin-top:16px">
              <p class="drawer-section-title">Control de acceso</p>

              @if (confirmDeactivate()) {
                <div class="confirm-panel">
                  <p class="confirm-message">
                    ¿Desactivar "{{ selectedTenant()!.name }}"? Los usuarios de este tenant
                    perderán acceso inmediatamente.
                  </p>
                  <div class="confirm-actions">
                    <button mat-flat-button color="warn" (click)="confirmAndDeactivate()">
                      Sí, desactivar
                    </button>
                    <button mat-stroked-button (click)="confirmDeactivate.set(false)">
                      Cancelar
                    </button>
                  </div>
                </div>
              } @else {
                <div class="action-buttons">
                  @if (selectedTenant()!.isActive) {
                    <button
                      mat-stroked-button
                      color="warn"
                      [disabled]="tenantsStore.isSaving()"
                      (click)="confirmDeactivate.set(true)">
                      <mat-icon>block</mat-icon>
                      Desactivar tenant
                    </button>
                  } @else {
                    <button
                      mat-flat-button
                      color="primary"
                      [disabled]="tenantsStore.isSaving()"
                      (click)="activate()">
                      <mat-icon>check_circle</mat-icon>
                      Activar tenant
                    </button>
                  }
                </div>
              }
            </div>

          </div>
        </div>
      }

    </div>
  `,
})
export class AdminComponent implements OnInit {
  readonly tenantsStore = inject(TenantsStore);

  planFilter   = signal<PlanTier | null>(null);
  activeFilter = signal<ActiveFilter>('all');
  selectedTenant = signal<TenantProfile | null>(null);
  confirmDeactivate = signal(false);
  pendingPlan: PlanTier = 'Free';

  readonly planTiers: PlanTier[] = ['Free', 'Starter', 'Professional', 'Enterprise'];
  readonly planColors = PLAN_TIER_COLORS;
  readonly planLabels = PLAN_TIER_LABELS;
  readonly displayedColumns = ['name', 'plan', 'status', 'vehicles', 'users', 'createdAt'];

  filteredTenants = computed(() => {
    let items = this.tenantsStore.entities();
    const plan = this.planFilter();
    const active = this.activeFilter();
    if (plan)            items = items.filter(t => t.planTier === plan);
    if (active === 'active')   items = items.filter(t => t.isActive);
    if (active === 'inactive') items = items.filter(t => !t.isActive);
    return items;
  });

  ngOnInit(): void {
    if (this.tenantsStore.totalCount() === 0) {
      this.tenantsStore.loadAll();
    }
  }

  select(t: TenantProfile): void {
    const isSame = this.selectedTenant()?.id === t.id;
    this.selectedTenant.set(isSame ? null : t);
    this.confirmDeactivate.set(false);
    if (!isSame) this.pendingPlan = t.planTier;
  }

  async savePlan(): Promise<void> {
    const t = this.selectedTenant();
    if (!t) return;
    try {
      await this.tenantsStore.updatePlan(t.id, this.pendingPlan);
      this.selectedTenant.set({ ...t, planTier: this.pendingPlan });
    } catch {
      /* error shown via store.error() */
    }
  }

  async confirmAndDeactivate(): Promise<void> {
    const t = this.selectedTenant();
    if (!t) return;
    try {
      await this.tenantsStore.setActive(t.id, false);
      this.selectedTenant.set({ ...t, isActive: false });
      this.confirmDeactivate.set(false);
    } catch {
      /* error shown via store.error() */
    }
  }

  async activate(): Promise<void> {
    const t = this.selectedTenant();
    if (!t) return;
    try {
      await this.tenantsStore.setActive(t.id, true);
      this.selectedTenant.set({ ...t, isActive: true });
    } catch {
      /* error shown via store.error() */
    }
  }

  usagePct(current: number | undefined, max: number): number {
    if (!max) return 0;
    return Math.min(100, Math.round(((current ?? 0) / max) * 100));
  }

  usageColor(current: number | undefined, max: number): string {
    const pct = this.usagePct(current, max);
    if (pct >= 90) return '#F44336';
    if (pct >= 70) return '#FF9800';
    return '#4CAF50';
  }
}
