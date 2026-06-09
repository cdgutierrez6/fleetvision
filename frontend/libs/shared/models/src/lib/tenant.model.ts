export type PlanTier = 'Free' | 'Starter' | 'Professional' | 'Enterprise';

export interface TenantProfile {
  id: string;
  name: string;
  contactEmail: string;
  planTier: PlanTier;
  isActive: boolean;
  maxVehicles: number;
  maxUsers: number;
  currentVehicles?: number;
  currentUsers?: number;
  createdAt: string;
}

export interface UpdateTenantPlanRequest {
  planTier: PlanTier;
}

export const PLAN_TIER_COLORS: Record<PlanTier, string> = {
  Free:         '#9E9E9E',
  Starter:      '#2196F3',
  Professional: '#4CAF50',
  Enterprise:   '#9C27B0',
};

export const PLAN_TIER_LABELS: Record<PlanTier, string> = {
  Free:         'Gratuito',
  Starter:      'Inicial',
  Professional: 'Profesional',
  Enterprise:   'Empresarial',
};
