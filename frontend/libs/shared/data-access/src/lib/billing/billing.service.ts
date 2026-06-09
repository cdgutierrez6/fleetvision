import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PlanTier } from '@fleetvision/shared/models';
import { SubscriptionDto } from '@fleetvision/shared/models';

@Injectable({ providedIn: 'root' })
export class BillingService {
  private http = inject(HttpClient);

  getSubscription(): Observable<SubscriptionDto> {
    return this.http.get<SubscriptionDto>('/api/billing/subscription');
  }

  createCheckoutSession(plan: PlanTier): Observable<{ sessionUrl: string }> {
    return this.http.post<{ sessionUrl: string }>('/api/billing/checkout', { plan });
  }

  createPortalSession(returnUrl: string): Observable<{ portalUrl: string }> {
    return this.http.post<{ portalUrl: string }>('/api/billing/portal', { returnUrl });
  }

  cancelSubscription(): Observable<void> {
    return this.http.delete<void>('/api/billing/subscription');
  }
}
