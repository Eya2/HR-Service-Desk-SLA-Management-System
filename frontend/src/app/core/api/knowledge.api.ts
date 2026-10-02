import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Article, ArticleSummary, SaveArticle } from './api.models';

/** Help centre articles. */
@Injectable({ providedIn: 'root' })
export class KnowledgeApi {
  private readonly http = inject(HttpClient);

  list(query: string | null, includeUnpublished = false): Observable<ArticleSummary[]> {
    let params = new HttpParams();
    if (query?.trim()) params = params.set('q', query.trim());
    if (includeUnpublished) params = params.set('includeUnpublished', true);
    return this.http.get<ArticleSummary[]>('/api/knowledge', { params });
  }

  /** Up to five published articles matching what is being typed. */
  suggest(text: string): Observable<ArticleSummary[]> {
    return this.http.get<ArticleSummary[]>('/api/knowledge/suggest', { params: { text } });
  }

  get(id: string): Observable<Article> {
    return this.http.get<Article>(`/api/knowledge/${encodeURIComponent(id)}`);
  }

  markHelpful(id: string): Observable<void> {
    return this.http.post<void>(`/api/knowledge/${encodeURIComponent(id)}/helpful`, null);
  }

  save(id: string | null, article: SaveArticle): Observable<Article> {
    return id
      ? this.http.put<Article>(`/api/knowledge/${encodeURIComponent(id)}`, article)
      : this.http.post<Article>('/api/knowledge', article);
  }
}
