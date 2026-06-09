export interface Vehicle {
  id: string;
  tenantId: string;
  plateNumber: string;
  make: string;
  model: string;
  year: number;
  status: 'Active' | 'Inactive' | 'Maintenance';
  currentLatitude?: number;
  currentLongitude?: number;
  lastSeenAt?: string;
}
