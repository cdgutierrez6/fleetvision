import { PlanTier } from './tenant.model';

export type SubscriptionStatus = 'Active' | 'Trialing' | 'PastDue' | 'Canceled';

export interface SubscriptionDto {
  tenantId:             string;
  plan:                 PlanTier;
  status:               SubscriptionStatus;
  currentPeriodEnd:     string | null;
  cancelAtPeriodEnd:    boolean;
  stripeSubscriptionId: string | null;
}

export interface PlanDefinition {
  tier:        PlanTier;
  priceMonthly: number;
  maxVehicles: number;
  maxUsers:    number;
  features:    string[];
  highlight:   boolean;
}

export const PLAN_DEFINITIONS: PlanDefinition[] = [
  {
    tier:         'Free',
    priceMonthly: 0,
    maxVehicles:  5,
    maxUsers:     2,
    features:     ['5 vehículos', '2 usuarios', 'Telemetría básica', 'Alertas en tiempo real'],
    highlight:    false,
  },
  {
    tier:         'Starter',
    priceMonthly: 49,
    maxVehicles:  25,
    maxUsers:     10,
    features:     ['25 vehículos', '10 usuarios', 'Geofencing', 'Reportes mensuales', 'Soporte email'],
    highlight:    false,
  },
  {
    tier:         'Professional',
    priceMonthly: 149,
    maxVehicles:  100,
    maxUsers:     50,
    features:     ['100 vehículos', '50 usuarios', 'Mantenimiento predictivo', 'API acceso completo', 'Soporte prioritario'],
    highlight:    true,
  },
  {
    tier:         'Enterprise',
    priceMonthly: 499,
    maxVehicles:  -1,
    maxUsers:     -1,
    features:     ['Vehículos ilimitados', 'Usuarios ilimitados', 'SLA 99.9%', 'Integración ERP', 'Soporte 24/7 dedicado'],
    highlight:    false,
  },
];

export const SUBSCRIPTION_STATUS_COLORS: Record<SubscriptionStatus, string> = {
  Active:   '#4CAF50',
  Trialing: '#2196F3',
  PastDue:  '#FF9800',
  Canceled: '#9E9E9E',
};

export const SUBSCRIPTION_STATUS_LABELS: Record<SubscriptionStatus, string> = {
  Active:   'Activa',
  Trialing: 'En prueba',
  PastDue:  'Pago pendiente',
  Canceled: 'Cancelada',
};
