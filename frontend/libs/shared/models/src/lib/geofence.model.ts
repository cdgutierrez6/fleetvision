export interface GeofenceCoordinate {
  latitude: number;
  longitude: number;
}

export interface Geofence {
  id: string;
  tenantId: string;
  name: string;
  type: 'Inclusion' | 'Exclusion';
  coordinates: GeofenceCoordinate[];
  isActive: boolean;
}
