import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CartaoPontoArquivo, CartaoPontoFuncionario, CartaoPontoRegistro } from './models';

const API_URL = '/api';

@Injectable({ providedIn: 'root' })
export class CartaoPontoService {
  constructor(private readonly http: HttpClient) {}

  listArquivos(): Observable<CartaoPontoArquivo[]> {
    return this.http.get<CartaoPontoArquivo[]>(`${API_URL}/cartaoponto/arquivos`);
  }

  importar(arquivo: File): Observable<CartaoPontoArquivo> {
    const formData = new FormData();
    formData.append('arquivo', arquivo, arquivo.name);
    return this.http.post<CartaoPontoArquivo>(`${API_URL}/cartaoponto/importar`, formData);
  }

  listFuncionarios(arquivoId?: number | null): Observable<CartaoPontoFuncionario[]> {
    return this.http.get<CartaoPontoFuncionario[]>(`${API_URL}/cartaoponto/funcionarios`, {
      params: this.params(arquivoId),
    });
  }

  listRegistros(cpf: string, arquivoId?: number | null): Observable<CartaoPontoRegistro[]> {
    return this.http.get<CartaoPontoRegistro[]>(`${API_URL}/cartaoponto/funcionarios/${cpf}/registros`, {
      params: this.params(arquivoId),
    });
  }

  updateRegistro(id: number, horarioEditado: string): Observable<CartaoPontoRegistro> {
    return this.http.put<CartaoPontoRegistro>(`${API_URL}/cartaoponto/registros/${id}`, { horarioEditado });
  }

  responder(cpf: string, precisaAjuste: boolean, arquivoId?: number | null, mes?: string | null): Observable<{ sucesso: boolean }> {
    return this.http.post<{ sucesso: boolean }>(
      `${API_URL}/cartaoponto/funcionarios/${cpf}/resposta`,
      { precisaAjuste },
      { params: mes ? this.params(arquivoId).set('mes', mes) : this.params(arquivoId) },
    );
  }

  private params(arquivoId?: number | null): HttpParams {
    return arquivoId ? new HttpParams().set('arquivoId', arquivoId) : new HttpParams();
  }
}
