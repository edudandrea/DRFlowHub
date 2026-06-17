import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { SolicitacaoCompra, SolicitacaoCompraComunicação, SolicitacaoCompraPayload, SolicitacaoCompraUpdatePayload } from './models';

const API_URL = '/api';

interface SaveResponse {
  solicitacao: SolicitacaoCompra;
}

interface CommunicationResponse {
  sucesso: boolean;
  mensagem: string;
  comunicacao: SolicitacaoCompraComunicação;
}

@Injectable({ providedIn: 'root' })
export class ComprasService {
  constructor(private readonly http: HttpClient) {}

  list(): Observable<SolicitacaoCompra[]> {
    return this.http.get<SolicitacaoCompra[]>(`${API_URL}/solicitacoescompra`);
  }

  create(payload: SolicitacaoCompraPayload, documento?: File | null): Observable<SolicitacaoCompra> {
    const formData = new FormData();
    Object.entries(payload).forEach(([key, value]) => formData.append(key, value == null ? '' : String(value)));
    if (documento) {
      formData.append('documento', documento);
    }

    return this.http.post<SaveResponse>(`${API_URL}/solicitacoescompra`, formData).pipe(map((response) => response.solicitacao));
  }

  update(id: number, payload: SolicitacaoCompraUpdatePayload): Observable<SolicitacaoCompra> {
    return this.http.put<SaveResponse>(`${API_URL}/solicitacoescompra/${id}`, payload).pipe(map((response) => response.solicitacao));
  }

  approve(id: number, aprovada: boolean, observacoesAprovacao: string): Observable<SolicitacaoCompra> {
    return this.http
      .post<SaveResponse>(`${API_URL}/solicitacoescompra/${id}/aprovacao`, { aprovada, observacoesAprovacao })
      .pipe(map((response) => response.solicitacao));
  }

  listComunicacoes(id: number): Observable<SolicitacaoCompraComunicação[]> {
    return this.http.get<SolicitacaoCompraComunicação[]>(`${API_URL}/solicitacoescompra/${id}/comunicacoes`);
  }

  sendComunicação(id: number, mensagem: string): Observable<SolicitacaoCompraComunicação> {
    return this.http
      .post<CommunicationResponse>(`${API_URL}/solicitacoescompra/${id}/comunicacoes`, { mensagem })
      .pipe(map((response) => response.comunicacao));
  }

  downloadDocument(id: number): Observable<Blob> {
    return this.http.get(`${API_URL}/solicitacoescompra/${id}/documento`, { responseType: 'blob' });
  }
}
