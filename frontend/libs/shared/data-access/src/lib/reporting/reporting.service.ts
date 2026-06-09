import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface FleetKpisDto {
  activeVehicles:       number;
  totalDistanceKm:      number;
  avgSpeedKmh:          number;
  maxSpeedKmh:          number;
  totalPositionRecords: number;
  periodStart:          string;
  periodEnd:            string;
}

export interface ViolationByTypeDto {
  violationType: string;
  count:         number;
  percentage:    number;
}

export interface ViolationByVehicleDto {
  vehicleId: string;
  count:     number;
}

export interface ViolationsSummaryDto {
  totalViolations: number;
  byType:          ViolationByTypeDto[];
  topVehicles:     ViolationByVehicleDto[];
}

export interface FleetStatusDto {
  total:       number;
  active:      number;
  maintenance: number;
  inactive:    number;
}

export type DateRange = '7d' | '30d' | '90d';

@Injectable({ providedIn: 'root' })
export class ReportingService {
  private http = inject(HttpClient);

  getFleetKpis(range: DateRange): Observable<FleetKpisDto> {
    return this.http.get<FleetKpisDto>(`/api/reports/fleet-kpis?range=${range}`);
  }

  getViolationsSummary(range: DateRange): Observable<ViolationsSummaryDto> {
    return this.http.get<ViolationsSummaryDto>(`/api/reports/violations-summary?range=${range}`);
  }

  getFleetStatus(): Observable<FleetStatusDto> {
    return this.http.get<FleetStatusDto>('/api/reports/fleet-status');
  }

  exportPdf(tenantName: string, range: DateRange): Observable<Blob> {
    return this.http.post('/api/reports/export/pdf',
      { tenantName, range },
      { responseType: 'blob' });
  }
}
