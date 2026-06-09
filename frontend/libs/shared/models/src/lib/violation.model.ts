export type ViolationType =
  | 'SpeedExceeded'
  | 'EnteredForbiddenZone'
  | 'ExitedAllowedZone'
  | 'OutsideSchedule';

export interface ViolationEvent {
  id: string;
  vehicleId: string;
  driverId?: string;
  geofenceId: string;
  geofenceName: string;
  violationType: ViolationType;
  latitude: number;
  longitude: number;
  actualSpeedKmh?: number;
  limitSpeedKmh?: number;
  occurredAt: string;
}

export const VIOLATION_TYPE_LABELS: Record<ViolationType, string> = {
  SpeedExceeded: 'Velocidad excedida',
  EnteredForbiddenZone: 'Zona prohibida',
  ExitedAllowedZone: 'Salió de zona permitida',
  OutsideSchedule: 'Fuera de horario',
};

export const VIOLATION_TYPE_ICONS: Record<ViolationType, string> = {
  SpeedExceeded: 'speed',
  EnteredForbiddenZone: 'location_off',
  ExitedAllowedZone: 'logout',
  OutsideSchedule: 'schedule',
};
