import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface CatalogProductDto {
  id: string;
  name: string;
  slug: string;
  description: string;
  price: number;
  imageUrl: string | null;
  badge: string | null;
  isFeatured: boolean;
  categoryName: string;
  categorySlug: string;
}

export interface CatalogCategoryDto {
  id: string;
  name: string;
  slug: string;
  sortOrder: number;
}

@Injectable({ providedIn: 'root' })
export class CatalogApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1';
  getProducts(): Observable<CatalogProductDto[]> {
    return this.http.get<CatalogProductDto[]>(`${this.baseUrl}/products`);
  }
  getCategories(): Observable<CatalogCategoryDto[]> {
    return this.http.get<CatalogCategoryDto[]>(`${this.baseUrl}/categories`);
  }
}
