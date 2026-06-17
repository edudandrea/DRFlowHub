import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

const API_URL = '/api';

export interface PecaVendaMensal {
  mes: string;
  faturamento: number;
  margem: number;
  rentabilidade: number;
  rentabilidadePercentual: number;
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
  rentabilidade: number;
  rentabilidadePercentual: number;
  giroDias: number;
}

export interface PecaVendedor {
  nome: string;
  cpfVendedor: string;
  faturamento: number;
  pedidos: number;
  conversaoPercentual: number;
  metaVendas: number;
  metaAtual: number;
  metaMesAnterior: number;
  metaDataInicio?: string | null;
  metaDataFim?: string | null;
  vendasDiarias: PecaVendaDiariaVendedor[];
  metasMensais: PecaVendedorMetaMensal[];
}

export interface PecaVendaDiariaVendedor {
  data: string;
  faturamento: number;
  pedidos: number;
}

export interface PecaVendedorMetaMensal {
  mes: string;
  valorMeta: number;
}

export interface PecaCanal {
  nome: string;
  faturamento: number;
  clientesAtendidos: number;
}

export interface PecaCanalDetalhe {
  canal: string;
  cliente: string;
  numeroNotaFiscal: string;
  data: string;
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
  empresasPermitidas?: number[] | null;
  revendasPermitidas?: number[] | null;
  vendasMensais: PecaVendaMensal[];
  categorias: PecaCategoria[];
  pecas: PecaRanking[];
  vendedores: PecaVendedor[];
  canais: PecaCanal[];
  clientes: PecaCliente[];
  seguradoras: PecaCliente[];
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

  load(filter: { dataInicio?: string; dataFim?: string; rankingDataInicio?: string; rankingDataFim?: string; empresa?: number | null; revenda?: number | number[] | null; canal?: string | string[] } = {}): Observable<PecasBiData> {
    const params = this.buildParams(filter);
    return this.http.get<PecasBiData>(`${API_URL}/pecas-bi`, { params });
  }

  loadCanalDetalhes(canal: string, filter: { dataInicio?: string; dataFim?: string; empresa?: number | null; revenda?: number | number[] | null; canal?: string | string[] } = {}): Observable<PecaCanalDetalhe[]> {
    const params = this.buildParams(filter);
    return this.http.get<PecaCanalDetalhe[]>(`${API_URL}/pecas-bi/canais/${encodeURIComponent(canal)}/detalhes`, { params });
  }

  saveMeta(payload: PecaVendedorMetaPayload): Observable<PecaVendedorMetaPayload> {
    return this.http.put<PecaVendedorMetaPayload>(`${API_URL}/pecas-bi/vendedores/meta`, payload);
  }

  private buildParams(filter: { dataInicio?: string; dataFim?: string; rankingDataInicio?: string; rankingDataFim?: string; empresa?: number | null; revenda?: number | number[] | null; canal?: string | string[] } = {}): Record<string, string> {
    const params: Record<string, string> = {};
    if (filter.dataInicio) {
      params['dataInicio'] = filter.dataInicio;
    }
    if (filter.dataFim) {
      params['dataFim'] = filter.dataFim;
    }
    if (filter.rankingDataInicio) {
      params['rankingDataInicio'] = filter.rankingDataInicio;
    }
    if (filter.rankingDataFim) {
      params['rankingDataFim'] = filter.rankingDataFim;
    }
    if (filter.empresa) {
      params['empresa'] = String(filter.empresa);
    }
    if (Array.isArray(filter.revenda) && filter.revenda.length) {
      params['revenda'] = filter.revenda.join(',');
    } else if (!Array.isArray(filter.revenda) && filter.revenda) {
      params['revenda'] = String(filter.revenda);
    }
    if (Array.isArray(filter.canal) && filter.canal.length) {
      params['canal'] = filter.canal.join(',');
    } else if (!Array.isArray(filter.canal) && filter.canal && filter.canal !== 'Todos') {
      params['canal'] = filter.canal;
    }

    return params;
  }
}
