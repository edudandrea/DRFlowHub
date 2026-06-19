import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import {
  GestaoPessoasEtapa,
  GestaoPessoasEtapaPayload,
  GestaoPessoasProcesso,
  GestaoPessoasProcessoPayload,
  GestaoPessoasTipoProcesso,
} from './models';

const API_URL = '/api/gestaopessoas';

interface EtapaResponse {
  sucesso: boolean;
  mensagem: string;
  etapa: GestaoPessoasEtapa;
}

interface ProcessoResponse {
  sucesso: boolean;
  mensagem: string;
  processo: GestaoPessoasProcesso;
}

@Injectable({ providedIn: 'root' })
export class GestaoPessoasService {
  constructor(private readonly http: HttpClient) {}

  listEtapas(tipoProcesso?: GestaoPessoasTipoProcesso): Observable<GestaoPessoasEtapa[]> {
    const query = tipoProcesso ? `?tipoProcesso=${tipoProcesso}` : '';
    return this.http.get<GestaoPessoasEtapa[]>(`${API_URL}/etapas${query}`);
  }

  saveEtapa(payload: GestaoPessoasEtapaPayload, id?: number | null): Observable<GestaoPessoasEtapa> {
    const request = id
      ? this.http.put<EtapaResponse>(`${API_URL}/etapas/${id}`, payload)
      : this.http.post<EtapaResponse>(`${API_URL}/etapas`, payload);
    return request.pipe(map((response) => response.etapa));
  }

  deleteEtapa(id: number): Observable<void> {
    return this.http.delete<void>(`${API_URL}/etapas/${id}`);
  }

  listProcessos(): Observable<GestaoPessoasProcesso[]> {
    return this.http.get<GestaoPessoasProcesso[]>(`${API_URL}/processos`);
  }

  createProcesso(payload: GestaoPessoasProcessoPayload): Observable<GestaoPessoasProcesso> {
    return this.http
      .post<ProcessoResponse>(`${API_URL}/processos`, payload)
      .pipe(map((response) => response.processo));
  }

  advance(id: number, observacoes = ''): Observable<GestaoPessoasProcesso> {
    return this.http
      .post<ProcessoResponse>(`${API_URL}/processos/${id}/avancar`, { observacoes })
      .pipe(map((response) => response.processo));
  }

  back(id: number, observacoes = ''): Observable<GestaoPessoasProcesso> {
    return this.http
      .post<ProcessoResponse>(`${API_URL}/processos/${id}/voltar`, { observacoes })
      .pipe(map((response) => response.processo));
  }

  cancel(id: number, motivoCancelamento: string): Observable<GestaoPessoasProcesso> {
    return this.http
      .post<ProcessoResponse>(`${API_URL}/processos/${id}/cancelar`, { motivoCancelamento })
      .pipe(map((response) => response.processo));
  }
}
