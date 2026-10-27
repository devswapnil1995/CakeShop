import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

export interface HeroBannerDto {
  id: string;
  title: string;
  subtitle: string;
  textPosition: 'top' | 'bottom' | 'left' | 'right';
  mediaType: 'Image' | 'Video';
  mediaUrl: string;
  mobileMediaUrl: string | null;
  buttonText: string | null;
  buttonUrl: string | null;
  sortOrder: number;
  startsUtc: string | null;
  endsUtc: string | null;
}

export interface StoreProfileDto {
  clientId: string; name: string; slug: string; tagline: string; storyTitle: string; story: string;
  storyImageUrl: string | null; logoUrl: string | null; phone: string | null; whatsAppNumber: string | null;
  email: string | null; address: string | null; openingHours: string | null; instagramUrl: string | null;
  facebookUrl: string | null; websiteUrl: string | null;
}

export interface StoreCollectionDto {
  id: string; kind: 'Hamper' | 'Seasonal'; title: string; slug: string; description: string;
  highlights: string; imageUrl: string | null; navigationLabel: string | null;
  showInNavigation: boolean; bundlePrice: number | null;
  sortOrder: number; startsUtc: string | null; endsUtc: string | null;
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
  getStoreProfile(): Observable<StoreProfileDto> {
    return this.http.get<StoreProfileDto>(`${this.baseUrl}/store-profile`);
  }
  getHeroBanners(): Observable<HeroBannerDto[]> {
    return this.http.get<HeroBannerDto[]>(`${this.baseUrl}/hero-banners`);
  }
  getCollections(kind?: 'Hamper' | 'Seasonal'): Observable<StoreCollectionDto[]> {
    const suffix = kind ? `?kind=${encodeURIComponent(kind)}` : '';
    return this.http.get<StoreCollectionDto[]>(`${this.baseUrl}/collections${suffix}`);
  }
  getProducts(): Observable<CatalogProductDto[]> {
    return this.http.get<CatalogProductDto[]>(`${this.baseUrl}/products`);
  }
  getCategories(): Observable<CatalogCategoryDto[]> {
    return this.http.get<CatalogCategoryDto[]>(`${this.baseUrl}/categories`);
  }
}
