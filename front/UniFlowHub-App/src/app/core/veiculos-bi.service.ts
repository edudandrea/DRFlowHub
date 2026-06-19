import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

const API_URL = '/api';

type VeiculosBiFilter = {
  dataInicio?: string;
  dataFim?: string;
  empresa?: number | null;
  revenda?: Array<number | string> | null;
};

export interface VeiculoAcessorioRanking {
  codigo: string;
  cpfVendedor: string;
  nome: string;
  categoria: string;
  quantidade: number;
  faturamento: number;
  margemPercentual: number;
  rentabilidade: number;
  meta: number;
  tipoMeta: 'valor' | 'quantidade';
  metaDataInicio?: string | null;
  metaDataFim?: string | null;
}

export interface VeiculosBiDashboard {
  filiais: VeiculoBiFilialVenda[];
  vendasDiarias: VeiculoBiVendaDiaria[];
  vendasDetalhes: VeiculoBiVendaDetalhe[];
  modelos: VeiculoBiModeloRanking[];
  vendedores: VeiculoBiVendedorMeta[];
  atualizadoEm: string;
}

export interface VeiculoBiFilialVenda {
  empresaNumero: number;
  empresaNome: string;
  revendaNumero: number;
  filial: string;
  metaNovos: number;
  metaVendaDireta: number;
  anunciadosNovos: number;
  faturadosNovos: number;
  anunciadosDireta: number;
  faturadosDireta: number;
  seminovos: number;
  propostas: number;
  baixados: number;
  faturamento: number;
  margem: number;
  faturamentoSemDireta: number;
  margemSemDireta: number;
}

export interface VeiculoBiVendaDiaria {
  data: string;
  novos: number;
  vendaDireta: number;
  seminovos: number;
}

export interface VeiculoBiVendaDetalhe {
  data: string;
  tipo: string;
  cliente: string;
  notaFiscal: string;
  veiculo: string;
  valor: number;
}

export interface VeiculoBiModeloRanking {
  modelo: string;
  familia: string;
  unidades: number;
  faturamento: number;
  margemPercentual: number;
}

export interface VeiculoBiVendedorMeta {
  vendedor: string;
  cpfVendedor: string;
  filial: string;
  meta: number;
  tipoMeta: 'valor' | 'quantidade';
  realizado: number;
  faturamento: number;
  metaDataInicio?: string | null;
  metaDataFim?: string | null;
}

export interface VeiculoVendedorMetaPayload {
  cpfVendedor: string;
  nomeVendedor: string;
  origem: 'veiculos' | 'acessorios';
  tipoMeta: 'valor' | 'quantidade';
  valorMeta: number;
  dataInicio: string;
  dataFim: string;
}

export interface VeiculosBiRetornoFiDashboard {
  contratos: number;
  retornoTotal: number;
  valorFinanciado: number;
  valorVenda: number;
  comissaoTotal: number;
  financeiras: VeiculosBiRetornoFiGrupo[];
  vendedores: VeiculosBiRetornoFiGrupo[];
  parcelas: VeiculosBiRetornoFiGrupo[];
  atualizadoEm: string;
}

export interface VeiculosBiRetornoFiGrupo {
  nome: string;
  quantidade: number;
  retorno: number;
  valorFinanciado: number;
  comissao: number;
}

@Injectable({ providedIn: 'root' })
export class VeiculosBiService {
  constructor(private readonly http: HttpClient) {}

  loadDashboard(filter: VeiculosBiFilter = {}): Observable<VeiculosBiDashboard> {
    return this.http.get<VeiculosBiDashboard>(`${API_URL}/veiculos-bi/dashboard`, { params: this.buildParams(filter) });
  }

  loadAcessorios(filter: VeiculosBiFilter = {}): Observable<VeiculoAcessorioRanking[]> {
    return this.http.get<VeiculoAcessorioRanking[]>(`${API_URL}/veiculos-bi/acessorios`, { params: this.buildParams(filter) });
  }

  loadRetornoFi(filter: VeiculosBiFilter = {}): Observable<VeiculosBiRetornoFiDashboard> {
    return this.http.get<VeiculosBiRetornoFiDashboard>(`${API_URL}/veiculos-bi/retorno-fi`, { params: this.buildParams(filter) });
  }

  saveMeta(payload: VeiculoVendedorMetaPayload): Observable<VeiculoVendedorMetaPayload> {
    return this.http.put<VeiculoVendedorMetaPayload>(`${API_URL}/veiculos-bi/vendedores/meta`, payload);
  }

  private buildParams(filter: VeiculosBiFilter = {}): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value === undefined || value === null || value === '' || (Array.isArray(value) && !value.length)) {
        continue;
      }

      params = params.set(key, Array.isArray(value) ? value.join(',') : String(value));
    }

    return params;
  }
}
