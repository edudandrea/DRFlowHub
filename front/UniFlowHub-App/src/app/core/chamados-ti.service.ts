import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ChamadoTI, ChamadoTIComunicação, ChamadoTIPayload } from './models';

const API_URL = '/api';

interface SaveResponse {
  sucesso: boolean;
  mensagem: string;
  chamado: ChamadoTI;
}

interface CommunicationResponse {
  sucesso: boolean;
  mensagem: string;
  comunicacao: ChamadoTIComunicação;
}

@Injectable({ providedIn: 'root' })
export class ChamadosTIService {
  constructor(private readonly http: HttpClient) {}

  list(): Observable<ChamadoTI[]> {
    return this.http.get<ChamadoTI[]>(`${API_URL}/chamadosti`);
  }

  create(payload: ChamadoTIPayload, anexo?: File | null): Observable<ChamadoTI> {
    const formData = new FormData();
    Object.entries(payload).forEach(([key, value]) => {
      formData.append(key, String(value ?? ''));
    });

    if (anexo) {
      formData.append('anexo', anexo, anexo.name);
    }

    return this.http.post<SaveResponse>(`${API_URL}/chamadosti`, formData).pipe(map((response) => response.chamado));
  }

  update(id: number, payload: Omit<ChamadoTIPayload, 'userid'>): Observable<ChamadoTI> {
    return this.http.put<SaveResponse>(`${API_URL}/chamadosti/${id}`, payload).pipe(map((response) => response.chamado));
  }

  close(id: number, observacoesEncerramento: string): Observable<ChamadoTI> {
    return this.http
      .post<SaveResponse>(`${API_URL}/chamadosti/${id}/encerrar`, { observacoesEncerramento })
      .pipe(map((response) => response.chamado));
  }

  reopen(id: number): Observable<ChamadoTI> {
    return this.http.post<SaveResponse>(`${API_URL}/chamadosti/${id}/reabrir`, {}).pipe(map((response) => response.chamado));
  }

  listComunicacoes(id: number): Observable<ChamadoTIComunicação[]> {
    return this.http.get<ChamadoTIComunicação[]>(`${API_URL}/chamadosti/${id}/comunicacoes`);
  }

  sendComunicação(id: number, mensagem: string): Observable<ChamadoTIComunicação> {
    return this.http
      .post<CommunicationResponse>(`${API_URL}/chamadosti/${id}/comunicacoes`, { mensagem })
      .pipe(map((response) => response.comunicacao));
  }

  rateSatisfaction(id: number, nota: number, comentario: string): Observable<ChamadoTI> {
    return this.http
      .post<SaveResponse>(`${API_URL}/chamadosti/${id}/satisfacao`, { nota, comentario })
      .pipe(map((response) => response.chamado));
  }

  approve(id: number, observacoesAprovacao: string): Observable<ChamadoTI> {
    return this.http
      .post<SaveResponse>(`${API_URL}/chamadosti/${id}/aprovacao`, { observacoesAprovacao })
      .pipe(map((response) => response.chamado));
  }

  downloadImage(id: number): Observable<Blob> {
    return this.http.get(`${API_URL}/chamadosti/${id}/imagem`, { responseType: 'blob' });
  }

  downloadAttachment(id: number): Observable<Blob> {
    return this.http.get(`${API_URL}/chamadosti/${id}/anexo`, { responseType: 'blob' });
  }

  downloadComunicacoes(id: number): Observable<Blob> {
    return this.http.get(`${API_URL}/chamadosti/${id}/comunicacoes/download`, { responseType: 'blob' });
  }

  markComunicaçãoRead(id: number, comunicacaoId: number): Observable<ChamadoTIComunicação> {
    return this.http.post<ChamadoTIComunicação>(`${API_URL}/chamadosti/${id}/comunicacoes/${comunicacaoId}/lida`, {});
  }
}
