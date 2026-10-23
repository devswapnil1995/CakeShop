import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

export interface HeroBannerDto {
  id: string;
  title: string;
  subtitle: string;
  mediaType: 'Image' | 'Video';
  mediaUrl: string;
  mobileMediaUrl: string | null;
  buttonText: string | null;
  buttonUrl: string | null;
  sortOrder: number;
  startsUtc: string | null;
  endsUtc: string | null;
}

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
  variants: CatalogProductVariantDto[];
}

export interface CatalogCategoryDto {
  id: string;
  name: string;
  slug: string;
  sortOrder: number;
}


export interface ProductVariantDto {
  id: string;
  sizeLabel: string;
  weightInGrams: number;
  price: number;
  isAvailable: boolean;
}

export interface ProductVariant {
  id: string;
  sizeLabel: string;
  weightInGrams: number;
  price: number;
  isAvailable: boolean;
}

export interface CatalogProductVariantDto {
  id: string;
  sizeLabel: string;
  weightInGrams: number;
  price: number;
  isAvailable: boolean;
}

@Injectable({ providedIn: 'root' })
export class CatalogApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/v1`;
  getHeroBanners(): Observable<HeroBannerDto[]> {
    return this.http.get<HeroBannerDto[]>(`${this.baseUrl}/hero-banners`);
  }
  getProducts(): Observable<CatalogProductDto[]> {
    return this.http.get<CatalogProductDto[]>(`${this.baseUrl}/products`);
  }
  getCategories(): Observable<CatalogCategoryDto[]> {
    return this.http.get<CatalogCategoryDto[]>(`${this.baseUrl}/categories`);
  }
}
