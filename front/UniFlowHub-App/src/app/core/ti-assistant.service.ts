import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface TiAssistantGlobalSearchResponse {
  configured: boolean;
  answer: string;
  error: string;
}

@Injectable({ providedIn: 'root' })
export class TiAssistantService {
  constructor(private readonly http: HttpClient) {}

  searchGlobal(title: string, question: string, localContext: string): Observable<TiAssistantGlobalSearchResponse> {
    return this.http.post<TiAssistantGlobalSearchResponse>('/api/ti-assistant/global-search', {
      title,
      question,
      localContext,
    });
  }
}
