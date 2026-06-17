import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { SolicitacaoPayload, SolicitacaoRH, SolicitacaoRHComunicação } from './models';

const API_URL = '/api';

interface SaveResponse {
  sucesso: boolean;
  mensagem: string;
  solicitacao: SolicitacaoRH;
}

interface CommunicationResponse {
  sucesso: boolean;
  mensagem: string;
  comunicacao: SolicitacaoRHComunicação;
}

@Injectable({ providedIn: 'root' })
export class SolicitacoesService {
  constructor(private readonly http: HttpClient) {}

  list(): Observable<SolicitacaoRH[]> {
    return this.http.get<SolicitacaoRH[]>(`${API_URL}/solicitacoesrh`);
  }

  create(payload: SolicitacaoPayload, anexo?: File | null): Observable<SolicitacaoRH> {
    const formData = new FormData();
    Object.entries(payload).forEach(([key, value]) => {
      formData.append(key, String(value ?? ''));
    });

    if (anexo) {
      formData.append('anexo', anexo, anexo.name);
    }

    return this.http
      .post<SaveResponse>(`${API_URL}/solicitacoesrh`, formData)
      .pipe(map((response) => response.solicitacao));
  }

  update(id: number, payload: Omit<SolicitacaoPayload, 'userid'>): Observable<SolicitacaoRH> {
    return this.http
      .put<SaveResponse>(`${API_URL}/solicitacoesrh/${id}`, payload)
      .pipe(map((response) => response.solicitacao));
  }

  close(id: number, observacoesEncerramento: string): Observable<SolicitacaoRH> {
    return this.http
      .post<SaveResponse>(`${API_URL}/solicitacoesrh/${id}/encerrar`, { observacoesEncerramento })
      .pipe(map((response) => response.solicitacao));
  }

  reopen(id: number): Observable<SolicitacaoRH> {
    return this.http
      .post<SaveResponse>(`${API_URL}/solicitacoesrh/${id}/reabrir`, {})
      .pipe(map((response) => response.solicitacao));
  }

  rateSatisfaction(id: number, nota: number, comentario: string): Observable<SolicitacaoRH> {
    return this.http
      .post<SaveResponse>(`${API_URL}/solicitacoesrh/${id}/satisfacao`, { nota, comentario })
      .pipe(map((response) => response.solicitacao));
  }

  listComunicacoes(id: number): Observable<SolicitacaoRHComunicação[]> {
    return this.http.get<SolicitacaoRHComunicação[]>(`${API_URL}/solicitacoesrh/${id}/comunicacoes`);
  }

  sendComunicação(id: number, mensagem: string): Observable<SolicitacaoRHComunicação> {
    return this.http
      .post<CommunicationResponse>(`${API_URL}/solicitacoesrh/${id}/comunicacoes`, { mensagem })
      .pipe(map((response) => response.comunicacao));
  }

  downloadAttachment(id: number): Observable<Blob> {
    return this.http.get(`${API_URL}/solicitacoesrh/${id}/anexo`, { responseType: 'blob' });
  }
}
