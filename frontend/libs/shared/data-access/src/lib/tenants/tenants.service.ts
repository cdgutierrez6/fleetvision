import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { TenantProfile, PlanTier, UpdateTenantPlanRequest } from '@fleetvision/shared/models';

interface TenantProfileDto {
  id: string;
  tenantId: string;
  companyName: string;
  slug: string;
  plan: string;
  maxVehicles: number;
  maxUsers: number;
  billingEmail: string;
  isActive: boolean;
  createdAt: string;
}

interface PagedResult {
  items: TenantProfileDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class TenantsService {
  private http = inject(HttpClient);

  getAll(): Observable<TenantProfile[]> {
    return this.http.get<PagedResult>('/api/tenants?page=1&pageSize=100').pipe(
      map(res => res.items.map(this.toModel))
    );
  }

  getById(id: string): Observable<TenantProfile> {
    return this.http.get<TenantProfileDto>(`/api/tenants/${id}`).pipe(
      map(this.toModel)
    );
  }

  updatePlan(id: string, req: UpdateTenantPlanRequest): Observable<void> {
    const planInt: Record<PlanTier, number> = {
      Free: 0, Starter: 1, Professional: 2, Enterprise: 3
    };
    return this.http.patch<void>(`/api/tenants/${id}/plan`, { plan: planInt[req.planTier] });
  }

  setActive(id: string, isActive: boolean): Observable<void> {
    const action = isActive ? 'activate' : 'deactivate';
    return this.http.post<void>(`/api/tenants/${id}/${action}`, {});
  }

  private toModel(dto: TenantProfileDto): TenantProfile {
    return {
      id:            dto.tenantId,   // backend routes use tenantId, not profile id
      name:          dto.companyName,
      contactEmail:  dto.billingEmail,
      planTier:      dto.plan as PlanTier,
      isActive:      dto.isActive,
      maxVehicles:   dto.maxVehicles,
      maxUsers:      dto.maxUsers,
      createdAt:     dto.createdAt,
    };
  }
}
