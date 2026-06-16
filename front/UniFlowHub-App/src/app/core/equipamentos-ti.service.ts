import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { EquipamentoTI, EquipamentoTIPayload } from './models';

const API_URL = '/api';

interface SaveResponse {
  equipamento: EquipamentoTI;
}

@Injectable({ providedIn: 'root' })
export class EquipamentosTIService {
  constructor(private readonly http: HttpClient) {}

  list(): Observable<EquipamentoTI[]> {
    return this.http.get<EquipamentoTI[]>(`${API_URL}/equipamentosti`);
  }

  create(payload: EquipamentoTIPayload, documento?: File | null): Observable<EquipamentoTI> {
    const formData = new FormData();
    Object.entries(payload).forEach(([key, value]) => {
      if (value == null || (key === 'dataPrevistaRetorno' && value === '')) {
        return;
      }

      formData.append(key, String(value));
    });
    if (documento) {
      formData.append('documento', documento);
    }

    return this.http.post<SaveResponse>(`${API_URL}/equipamentosti`, formData).pipe(map((response) => response.equipamento));
  }

  update(id: number, payload: EquipamentoTIPayload): Observable<EquipamentoTI> {
    return this.http
      .put<SaveResponse>(`${API_URL}/equipamentosti/${id}`, this.normalizePayload(payload))
      .pipe(map((response) => response.equipamento));
  }

  delete(id: number, motivo: string): Observable<void> {
    return this.http.delete<void>(`${API_URL}/equipamentosti/${id}`, { body: { motivo } });
  }

  downloadDocument(id: number): Observable<Blob> {
    return this.http.get(`${API_URL}/equipamentosti/${id}/documento`, { responseType: 'blob' });
  }

  private normalizePayload(payload: EquipamentoTIPayload): EquipamentoTIPayload {
    return {
      ...payload,
      dataPrevistaRetorno: payload.dataPrevistaRetorno || null,
    } as EquipamentoTIPayload;
  }
}
