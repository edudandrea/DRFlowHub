import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { RepasseDashboard } from './models';

const API_URL = '/api';

export interface RepasseDashboardFilter {
  empresa?: number | null;
  revenda?: number | null;
  dataInicio?: string | null;
  dataFim?: string | null;
}

@Injectable({ providedIn: 'root' })
export class RepassesService {
  constructor(private readonly http: HttpClient) {}

  dashboard(filter: RepasseDashboardFilter): Observable<RepasseDashboard> {
    let params = new HttpParams();

    if (filter.empresa) {
      params = params.set('empresa', filter.empresa);
    }

    if (filter.revenda) {
      params = params.set('revenda', filter.revenda);
    }

    if (filter.dataInicio) {
      params = params.set('dataInicio', filter.dataInicio);
    }

    if (filter.dataFim) {
      params = params.set('dataFim', filter.dataFim);
    }

    return this.http.get<RepasseDashboard>(`${API_URL}/repasses/dashboard`, { params });
  }
}
