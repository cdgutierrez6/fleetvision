import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Geofence } from '@fleetvision/shared/models';

@Injectable({ providedIn: 'root' })
export class GeofencesService {
  private http = inject(HttpClient);

  getAll(): Observable<Geofence[]> {
    return this.http.get<Geofence[]>('/api/geofencing/geofences');
  }
}
