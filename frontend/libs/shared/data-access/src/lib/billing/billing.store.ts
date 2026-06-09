import { computed, inject } from '@angular/core';
import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { PlanTier } from '@fleetvision/shared/models';
import { SubscriptionDto } from '@fleetvision/shared/models';
import { BillingService } from './billing.service';

interface BillingState {
  subscription:  SubscriptionDto | null;
  isLoading:     boolean;
  isSaving:      boolean;
  error:         string | null;
}

const initialState: BillingState = {
  subscription: null,
  isLoading:    false,
  isSaving:     false,
  error:        null,
};

function isStripeUrl(url: string): boolean {
  try {
    const parsed = new URL(url);
    return parsed.protocol === 'https:' &&
           (parsed.hostname === 'checkout.stripe.com' ||
            parsed.hostname === 'billing.stripe.com' ||
            parsed.hostname.endsWith('.stripe.com'));
  } catch {
    return false;
  }
}

export const BillingStore = signalStore(
  { providedIn: 'root' },
  withState<BillingState>(initialState),
  withComputed(({ subscription }) => ({
    currentPlan:  computed(() => subscription()?.plan   ?? 'Free'),
    status:       computed(() => subscription()?.status ?? 'Active'),
    hasActiveSub: computed(() => {
      const s = subscription();
      return !!s?.stripeSubscriptionId && s.status !== 'Canceled';
    }),
    renewalDate: computed(() => {
      const end = subscription()?.currentPeriodEnd;
      return end ? new Date(end) : null;
    }),
  })),
  withMethods((store, service = inject(BillingService)) => ({

    async loadSubscription(): Promise<void> {
      patchState(store, { isLoading: true, error: null });
      try {
        const subscription = await firstValueFrom(service.getSubscription());
        patchState(store, { subscription, isLoading: false });
      } catch {
        patchState(store, { isLoading: false, error: 'Error al cargar la suscripción' });
      }
    },

    async startCheckout(plan: PlanTier): Promise<void> {
      patchState(store, { isSaving: true, error: null });
      try {
        const { sessionUrl } = await firstValueFrom(service.createCheckoutSession(plan));
        if (!isStripeUrl(sessionUrl)) {
          patchState(store, { isSaving: false, error: 'URL de pago inválida' });
          return;
        }
        patchState(store, { isSaving: false });
        window.location.href = sessionUrl;
      } catch {
        patchState(store, { isSaving: false, error: 'Error al iniciar el proceso de pago' });
        throw new Error('checkout failed');
      }
    },

    async openPortal(): Promise<void> {
      patchState(store, { isSaving: true, error: null });
      try {
        const returnUrl = window.location.href;
        const { portalUrl } = await firstValueFrom(service.createPortalSession(returnUrl));
        if (!isStripeUrl(portalUrl)) {
          patchState(store, { isSaving: false, error: 'URL del portal inválida' });
          return;
        }
        patchState(store, { isSaving: false });
        window.location.href = portalUrl;
      } catch {
        patchState(store, { isSaving: false, error: 'Error al abrir el portal de facturación' });
        throw new Error('portal failed');
      }
    },

    async cancelSubscription(): Promise<void> {
      patchState(store, { isSaving: true, error: null });
      try {
        await firstValueFrom(service.cancelSubscription());
        const current = store.subscription();
        if (current) {
          patchState(store, {
            subscription: { ...current, cancelAtPeriodEnd: true },
            isSaving: false,
          });
        } else {
          patchState(store, { isSaving: false });
        }
      } catch {
        patchState(store, { isSaving: false, error: 'Error al cancelar la suscripción' });
        throw new Error('cancel failed');
      }
    },

  }))
);
