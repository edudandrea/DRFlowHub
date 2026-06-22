import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import {
  GestaoPessoasEtapa,
  GestaoPessoasEtapaPayload,
  GestaoPessoasCargo,
  GestaoPessoasCargoPayload,
  GestaoPessoasColaborador,
  GestaoPessoasColaboradorPayload,
  GestaoPessoasColaboradorRetirada,
  GestaoPessoasColaboradorRetiradaPayload,
  GestaoPessoasItem,
  GestaoPessoasItemPayload,
  GestaoPessoasItemTipo,
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

interface CargoResponse {
  sucesso: boolean;
  mensagem: string;
  cargo: GestaoPessoasCargo;
}

interface ItemResponse {
  sucesso: boolean;
  mensagem: string;
  item: GestaoPessoasItem;
}

interface ColaboradorResponse {
  sucesso: boolean;
  mensagem: string;
  colaborador: GestaoPessoasColaborador;
}

interface RetiradaResponse {
  sucesso: boolean;
  mensagem: string;
  retirada: GestaoPessoasColaboradorRetirada;
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

  listCargos(): Observable<GestaoPessoasCargo[]> {
    return this.http.get<GestaoPessoasCargo[]>(`${API_URL}/cargos`);
  }

  saveCargo(payload: GestaoPessoasCargoPayload, id?: number | null): Observable<GestaoPessoasCargo> {
    const request = id
      ? this.http.put<CargoResponse>(`${API_URL}/cargos/${id}`, payload)
      : this.http.post<CargoResponse>(`${API_URL}/cargos`, payload);
    return request.pipe(map((response) => response.cargo));
  }

  listItens(tipo?: GestaoPessoasItemTipo): Observable<GestaoPessoasItem[]> {
    const query = tipo ? `?tipo=${tipo}` : '';
    return this.http.get<GestaoPessoasItem[]>(`${API_URL}/itens${query}`);
  }

  saveItem(payload: GestaoPessoasItemPayload, id?: number | null): Observable<GestaoPessoasItem> {
    const request = id
      ? this.http.put<ItemResponse>(`${API_URL}/itens/${id}`, payload)
      : this.http.post<ItemResponse>(`${API_URL}/itens`, payload);
    return request.pipe(map((response) => response.item));
  }

  listColaboradores(): Observable<GestaoPessoasColaborador[]> {
    return this.http.get<GestaoPessoasColaborador[]>(`${API_URL}/colaboradores`);
  }

  saveColaborador(payload: GestaoPessoasColaboradorPayload, id?: number | null): Observable<GestaoPessoasColaborador> {
    const request = id
      ? this.http.put<ColaboradorResponse>(`${API_URL}/colaboradores/${id}`, payload)
      : this.http.post<ColaboradorResponse>(`${API_URL}/colaboradores`, payload);
    return request.pipe(map((response) => response.colaborador));
  }

  addRetirada(colaboradorId: number, payload: GestaoPessoasColaboradorRetiradaPayload): Observable<GestaoPessoasColaboradorRetirada> {
    return this.http
      .post<RetiradaResponse>(`${API_URL}/colaboradores/${colaboradorId}/retiradas`, payload)
      .pipe(map((response) => response.retirada));
  }
}
