import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

const API_URL = '/api';

export interface PecaVendaMensal {
  mes: string;
  faturamento: number;
  margem: number;
  quantidade: number;
}

export interface PecaCategoria {
  nome: string;
  faturamento: number;
  margemPercentual: number;
}

export interface PecaRanking {
  nome: string;
  codigo: string;
  categoria: string;
  quantidade: number;
  faturamento: number;
  margemPercentual: number;
  giroDias: number;
}

export interface PecaVendedor {
  nome: string;
  cpfVendedor: string;
  faturamento: number;
  pedidos: number;
  conversaoPercentual: number;
  metaVendas: number;
  metaDataInicio?: string | null;
  metaDataFim?: string | null;
}

export interface PecaCanal {
  nome: string;
  faturamento: number;
}

export interface PecaCliente {
  nome: string;
  codigo: string;
  faturamento: number;
  notas: number;
}

export interface PecasBiData {
  atualizadoEm: string;
  podeVerRankingVendedores: boolean;
  vendasMensais: PecaVendaMensal[];
  categorias: PecaCategoria[];
  pecas: PecaRanking[];
  vendedores: PecaVendedor[];
  canais: PecaCanal[];
  clientes: PecaCliente[];
  minhaMeta?: PecaMetaResumo | null;
}

export interface PecaMetaResumo {
  valorVendido: number;
  valorMeta: number;
  dataInicio?: string | null;
  dataFim?: string | null;
}

export interface PecaVendedorMetaPayload {
  cpfVendedor: string;
  nomeVendedor: string;
  valorMeta: number;
  dataInicio: string;
  dataFim: string;
}

@Injectable({ providedIn: 'root' })
export class PecasBiService {
  constructor(private readonly http: HttpClient) {}

  load(filter: { dataInicio?: string; dataFim?: string; empresa?: number | null; revenda?: number | null; canal?: string } = {}): Observable<PecasBiData> {
    const params: Record<string, string> = {};
    if (filter.dataInicio) {
      params['dataInicio'] = filter.dataInicio;
    }
    if (filter.dataFim) {
      params['dataFim'] = filter.dataFim;
    }
    if (filter.empresa) {
      params['empresa'] = String(filter.empresa);
    }
    if (filter.revenda) {
      params['revenda'] = String(filter.revenda);
    }
    if (filter.canal && filter.canal !== 'Todos') {
      params['canal'] = filter.canal;
    }

    return this.http.get<PecasBiData>(`${API_URL}/pecas-bi`, { params });
  }

  saveMeta(payload: PecaVendedorMetaPayload): Observable<PecaVendedorMetaPayload> {
    return this.http.put<PecaVendedorMetaPayload>(`${API_URL}/pecas-bi/vendedores/meta`, payload);
  }
}
