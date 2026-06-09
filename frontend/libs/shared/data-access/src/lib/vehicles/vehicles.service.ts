import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Vehicle } from '@fleetvision/shared/models';

@Injectable({ providedIn: 'root' })
export class VehiclesService {
  private http = inject(HttpClient);

  getAll(): Observable<Vehicle[]> {
    return this.http.get<Vehicle[]>('/api/fleet-assets/vehicles');
  }
}
