import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { DatePipe, CurrencyPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialogModule } from '@angular/material/dialog';
import {
  PlanTier,
  PlanDefinition,
  PLAN_DEFINITIONS,
  PLAN_TIER_COLORS,
  PLAN_TIER_LABELS,
  SUBSCRIPTION_STATUS_COLORS,
  SUBSCRIPTION_STATUS_LABELS,
} from '@fleetvision/shared/models';
import { BillingStore, AuthStore } from '@fleetvision/shared/data-access';

@Component({
  selector: 'fv-billing',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    CurrencyPipe,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatDividerModule,
    MatTooltipModule,
    MatDialogModule,
  ],
  styles: [`
    :host { display: block; height: 100%; background: var(--fv-bg, #F5F7FA); overflow-y: auto; }

    /* ─── Page layout ──────────────────────────────────────────────── */
    .page { max-width: 1100px; margin: 0 auto; padding: 28px; }

    .page-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      margin-bottom: 28px;
      flex-wrap: wrap;
      gap: 12px;
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
      margin: 4px 0 0;
    }

    /* ─── Current subscription card ────────────────────────────────── */
    .sub-card {
      background: #fff;
      border: 1px solid var(--fv-border, #E0E4EA);
      border-radius: 14px;
      padding: 24px;
      margin-bottom: 32px;
      display: flex;
      align-items: center;
      gap: 20px;
      flex-wrap: wrap;
    }
    .sub-card-icon {
      width: 52px;
      height: 52px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }
    .sub-card-icon mat-icon { color: #fff; font-size: 26px; width: 26px; height: 26px; }

    .sub-card-info { flex: 1; min-width: 0; }
    .sub-plan-name {
      font-size: 20px;
      font-weight: 700;
      color: var(--fv-primary, #1E3A5F);
    }
    .sub-meta {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-top: 6px;
      flex-wrap: wrap;
    }

    .status-badge {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      padding: 3px 10px;
      border-radius: 20px;
      font-size: 12px;
      font-weight: 600;
    }
    .status-dot {
      width: 7px;
      height: 7px;
      border-radius: 50%;
    }

    .renewal-info {
      font-size: 13px;
      color: var(--fv-text-muted, #6B7280);
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .renewal-info mat-icon { font-size: 15px; width: 15px; height: 15px; }

    .cancel-warning {
      background: #FFF3E0;
      border: 1px solid #FFB74D;
      border-radius: 8px;
      padding: 8px 12px;
      font-size: 12px;
      color: #E65100;
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .cancel-warning mat-icon { font-size: 16px; width: 16px; height: 16px; color: #FF9800; }

    .sub-actions {
      display: flex;
      flex-direction: column;
      gap: 8px;
      align-items: flex-end;
    }

    /* ─── Plans grid ────────────────────────────────────────────────── */
    .plans-section { margin-bottom: 32px; }
    .section-title {
      font-size: 15px;
      font-weight: 700;
      color: var(--fv-primary, #1E3A5F);
      margin: 0 0 16px;
    }

    .plans-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 16px;
    }
    @media (max-width: 1050px) { .plans-grid { grid-template-columns: repeat(2, 1fr); } }
    @media (max-width: 600px)  { .plans-grid { grid-template-columns: 1fr; } }

    .plan-card {
      background: #fff;
      border: 2px solid var(--fv-border, #E0E4EA);
      border-radius: 14px;
      padding: 20px;
      display: flex;
      flex-direction: column;
      gap: 0;
      position: relative;
      transition: box-shadow .2s, border-color .2s;
    }
    .plan-card:hover { box-shadow: 0 4px 16px rgba(0,0,0,.08); }
    .plan-card.current {
      border-color: var(--plan-color);
      background: color-mix(in srgb, var(--plan-color) 4%, white);
    }
    .plan-card.highlight:not(.current) {
      border-color: var(--fv-accent, #00BFA5);
    }

    .popular-badge {
      position: absolute;
      top: -12px;
      left: 50%;
      transform: translateX(-50%);
      background: var(--fv-accent, #00BFA5);
      color: #fff;
      font-size: 11px;
      font-weight: 700;
      padding: 3px 12px;
      border-radius: 20px;
      white-space: nowrap;
    }

    .current-indicator {
      position: absolute;
      top: -12px;
      right: 12px;
      background: var(--plan-color);
      color: #fff;
      font-size: 11px;
      font-weight: 700;
      padding: 3px 10px;
      border-radius: 20px;
    }

    .plan-tier-label {
      font-size: 12px;
      font-weight: 700;
      letter-spacing: .5px;
      text-transform: uppercase;
      color: var(--plan-color);
      margin-bottom: 6px;
    }
    .plan-price {
      font-size: 28px;
      font-weight: 800;
      color: var(--fv-primary, #1E3A5F);
      line-height: 1;
    }
    .plan-price-period {
      font-size: 13px;
      color: var(--fv-text-muted, #6B7280);
      font-weight: 400;
    }

    mat-divider { margin: 14px 0; }

    .plan-features {
      list-style: none;
      padding: 0;
      margin: 0 0 16px;
      display: flex;
      flex-direction: column;
      gap: 8px;
      flex: 1;
    }
    .plan-feature {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 13px;
      color: var(--fv-text, #1A2332);
    }
    .plan-feature mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
      color: var(--fv-accent, #00BFA5);
      flex-shrink: 0;
    }

    .plan-action-btn {
      width: 100%;
      margin-top: auto;
    }

    /* ─── Error / loading ───────────────────────────────────────────── */
    .loading-state {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 12px;
      padding: 60px 0;
      color: var(--fv-text-muted, #6B7280);
      font-size: 14px;
    }

    .error-banner {
      background: #FFEBEE;
      border: 1px solid #EF9A9A;
      border-radius: 10px;
      padding: 14px 18px;
      display: flex;
      align-items: center;
      gap: 10px;
      color: #C62828;
      font-size: 13px;
      margin-bottom: 20px;
    }
    .error-banner mat-icon { flex-shrink: 0; }

    /* ─── Confirm cancel dialog ─────────────────────────────────────── */
    .confirm-overlay {
      position: fixed;
      inset: 0;
      background: rgba(0,0,0,.45);
      z-index: 1000;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .confirm-card {
      background: #fff;
      border-radius: 14px;
      padding: 28px;
      width: 420px;
      max-width: 90vw;
      box-shadow: 0 12px 40px rgba(0,0,0,.15);
    }
    .confirm-title {
      font-size: 18px;
      font-weight: 700;
      color: var(--fv-primary, #1E3A5F);
      margin: 0 0 10px;
    }
    .confirm-body {
      font-size: 14px;
      color: var(--fv-text-muted, #6B7280);
      line-height: 1.6;
      margin-bottom: 20px;
    }
    .confirm-actions {
      display: flex;
      justify-content: flex-end;
      gap: 10px;
    }
  `],
  template: `
    <div class="page">

      <!-- Header -->
      <div class="page-header">
        <div>
          <h1 class="page-title">Facturación y Planes</h1>
          <p class="page-subtitle">Gestiona tu suscripción y método de pago</p>
        </div>
        @if (billingStore.hasActiveSub()) {
          <button
            mat-stroked-button
            [disabled]="billingStore.isSaving()"
            (click)="onOpenPortal()">
            <mat-icon>open_in_new</mat-icon>
            Gestionar facturación
          </button>
        }
      </div>

      <!-- Error banner -->
      @if (billingStore.error()) {
        <div class="error-banner" role="alert">
          <mat-icon>error_outline</mat-icon>
          {{ billingStore.error() }}
        </div>
      }

      <!-- Loading -->
      @if (billingStore.isLoading()) {
        <div class="loading-state">
          <mat-spinner [diameter]="28" color="primary" />
          Cargando información de suscripción…
        </div>
      } @else {

        <!-- ─── Current subscription card ─────────────────────────── -->
        <div class="sub-card">
          <div
            class="sub-card-icon"
            [style.background]="planColor(billingStore.currentPlan())">
            <mat-icon>{{ planIcon(billingStore.currentPlan()) }}</mat-icon>
          </div>

          <div class="sub-card-info">
            <div class="sub-plan-name">
              Plan {{ planLabel(billingStore.currentPlan()) }}
            </div>
            <div class="sub-meta">
              <!-- Status badge -->
              <span
                class="status-badge"
                [style.background]="statusColor(billingStore.status()) + '22'"
                [style.color]="statusColor(billingStore.status())">
                <span
                  class="status-dot"
                  [style.background]="statusColor(billingStore.status())">
                </span>
                {{ statusLabel(billingStore.status()) }}
              </span>

              <!-- Renewal date -->
              @if (billingStore.renewalDate()) {
                <span class="renewal-info">
                  <mat-icon>event</mat-icon>
                  Renueva {{ billingStore.renewalDate() | date:'dd MMM yyyy' }}
                </span>
              }
            </div>

            <!-- Cancel warning -->
            @if (billingStore.subscription()?.cancelAtPeriodEnd) {
              <div class="cancel-warning" style="margin-top:10px">
                <mat-icon>warning</mat-icon>
                La suscripción se cancelará al final del período actual
              </div>
            }
          </div>

          <!-- Actions -->
          <div class="sub-actions">
            @if (billingStore.isSaving()) {
              <div style="display:flex;align-items:center;gap:8px;color:#6B7280;font-size:13px">
                <mat-spinner [diameter]="18" color="accent" />
                Procesando…
              </div>
            } @else if (billingStore.hasActiveSub() && !billingStore.subscription()?.cancelAtPeriodEnd) {
              <button
                mat-stroked-button
                color="warn"
                (click)="confirmCancel.set(true)"
                matTooltip="La suscripción permanecerá activa hasta el fin del período">
                <mat-icon>cancel</mat-icon>
                Cancelar suscripción
              </button>
            }
          </div>
        </div>

        <!-- ─── Plans grid ─────────────────────────────────────────── -->
        <div class="plans-section">
          <h2 class="section-title">Planes disponibles</h2>
          <div class="plans-grid">
            @for (plan of plans; track plan.tier) {
              <div
                class="plan-card"
                [class.current]="billingStore.currentPlan() === plan.tier"
                [class.highlight]="plan.highlight"
                [style.--plan-color]="planColor(plan.tier)">

                @if (plan.highlight) {
                  <div class="popular-badge">Más popular</div>
                }
                @if (billingStore.currentPlan() === plan.tier) {
                  <div class="current-indicator">Plan actual</div>
                }

                <div class="plan-tier-label">{{ planLabel(plan.tier) }}</div>

                <div class="plan-price">
                  @if (plan.priceMonthly === 0) {
                    <span>Gratis</span>
                  } @else {
                    <span>{{ plan.priceMonthly | currency:'USD':'symbol':'1.0-0' }}</span>
                    <span class="plan-price-period">/mes</span>
                  }
                </div>

                <mat-divider />

                <ul class="plan-features">
                  @for (feature of plan.features; track feature) {
                    <li class="plan-feature">
                      <mat-icon>check_circle</mat-icon>
                      {{ feature }}
                    </li>
                  }
                </ul>

                <!-- CTA button -->
                @if (billingStore.currentPlan() === plan.tier) {
                  <button mat-stroked-button class="plan-action-btn" disabled>
                    Plan actual
                  </button>
                } @else if (plan.tier === 'Free') {
                  <button
                    mat-stroked-button
                    color="warn"
                    class="plan-action-btn"
                    [disabled]="!billingStore.hasActiveSub() || billingStore.isSaving()"
                    (click)="confirmCancel.set(true)"
                    matTooltip="Cancela tu plan actual">
                    Bajar a gratuito
                  </button>
                } @else {
                  <button
                    mat-flat-button
                    color="primary"
                    class="plan-action-btn"
                    [disabled]="billingStore.isSaving()"
                    (click)="onStartCheckout(plan.tier)">
                    @if (isUpgrade(plan.tier)) {
                      Mejorar plan
                    } @else {
                      Cambiar plan
                    }
                  </button>
                }
              </div>
            }
          </div>
        </div>

      }

      <!-- ─── Cancel confirm overlay ─────────────────────────────── -->
      @if (confirmCancel()) {
        <div class="confirm-overlay" (click)="confirmCancel.set(false)">
          <div class="confirm-card" (click)="$event.stopPropagation()">
            <h3 class="confirm-title">Cancelar suscripción</h3>
            <p class="confirm-body">
              Tu plan permanecerá activo hasta el final del período de facturación actual.
              Después se cambiará al plan gratuito automáticamente.
              ¿Confirmas la cancelación?
            </p>
            <div class="confirm-actions">
              <button mat-stroked-button (click)="confirmCancel.set(false)">
                Volver
              </button>
              <button
                mat-flat-button
                color="warn"
                [disabled]="billingStore.isSaving()"
                (click)="onCancelSubscription()">
                Sí, cancelar
              </button>
            </div>
          </div>
        </div>
      }

    </div>
  `,
})
export class BillingComponent implements OnInit {
  readonly billingStore = inject(BillingStore);
  readonly authStore    = inject(AuthStore);

  readonly confirmCancel = signal(false);

  readonly plans: PlanDefinition[] = PLAN_DEFINITIONS;

  readonly planTierOrder: Record<PlanTier, number> = {
    Free:         0,
    Starter:      1,
    Professional: 2,
    Enterprise:   3,
  };

  ngOnInit(): void {
    this.billingStore.loadSubscription();
  }

  planColor(tier: PlanTier): string {
    return PLAN_TIER_COLORS[tier];
  }

  planLabel(tier: PlanTier): string {
    return PLAN_TIER_LABELS[tier];
  }

  statusColor(status: string): string {
    return SUBSCRIPTION_STATUS_COLORS[status as keyof typeof SUBSCRIPTION_STATUS_COLORS] ?? '#9E9E9E';
  }

  statusLabel(status: string): string {
    return SUBSCRIPTION_STATUS_LABELS[status as keyof typeof SUBSCRIPTION_STATUS_LABELS] ?? status;
  }

  planIcon(tier: PlanTier): string {
    const icons: Record<PlanTier, string> = {
      Free:         'rocket_launch',
      Starter:      'trending_up',
      Professional: 'workspace_premium',
      Enterprise:   'corporate_fare',
    };
    return icons[tier];
  }

  isUpgrade(target: PlanTier): boolean {
    const current = this.billingStore.currentPlan();
    return this.planTierOrder[target] > this.planTierOrder[current];
  }

  async onStartCheckout(plan: PlanTier): Promise<void> {
    try {
      await this.billingStore.startCheckout(plan);
    } catch {
      // error displayed via store.error()
    }
  }

  async onOpenPortal(): Promise<void> {
    try {
      await this.billingStore.openPortal();
    } catch {
      // error displayed via store.error()
    }
  }

  async onCancelSubscription(): Promise<void> {
    try {
      await this.billingStore.cancelSubscription();
      this.confirmCancel.set(false);
    } catch {
      this.confirmCancel.set(false);
    }
  }
}
