import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { BaseConhecimentoTI, BaseConhecimentoTIPayload } from './models';

const API_URL = '/api/base-conhecimento-ti';

interface SaveResponse {
  conhecimento: BaseConhecimentoTI;
}

@Injectable({ providedIn: 'root' })
export class BaseConhecimentoTIService {
  constructor(private readonly http: HttpClient) {}

  list(): Observable<BaseConhecimentoTI[]> {
    return this.http.get<BaseConhecimentoTI[]>(API_URL);
  }

  create(payload: BaseConhecimentoTIPayload, documento?: File | null): Observable<BaseConhecimentoTI> {
    return this.http.post<SaveResponse>(API_URL, this.toFormData(payload, documento)).pipe(map((response) => response.conhecimento));
  }

  update(id: number, payload: BaseConhecimentoTIPayload, documento?: File | null): Observable<BaseConhecimentoTI> {
    return this.http.put<SaveResponse>(`${API_URL}/${id}`, this.toFormData(payload, documento)).pipe(map((response) => response.conhecimento));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${API_URL}/${id}`);
  }

  downloadDocument(id: number): Observable<Blob> {
    return this.http.get(`${API_URL}/${id}/documento`, { responseType: 'blob' });
  }

  private toFormData(payload: BaseConhecimentoTIPayload, documento?: File | null): FormData {
    const formData = new FormData();
    Object.entries(payload).forEach(([key, value]) => formData.append(key, value == null ? '' : String(value)));
    if (documento) {
      formData.append('documento', documento, documento.name);
    }

    return formData;
  }
}
