import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface AdminCategory {
  id: string;
  name: string;
  slug: string;
  sortOrder: number;
  isActive: boolean;
}

export interface AdminProduct {
  id: string;
  categoryId: string;
  name: string;
  slug: string;
  description: string;
  price: number;
  imageUrl: string | null;
  badge: string | null;
  isFeatured: boolean;
  isAvailable: boolean;
  categoryName: string;
}

export interface ProductWrite {
  categoryId: string;
  name: string;
  slug: string;
  description: string;
  price: number;
  imageUrl: string | null;
  badge: string | null;
  isFeatured: boolean;
  isAvailable: boolean;
}

export interface CategoryWrite {
  name: string;
  slug: string;
  sortOrder: number;
  isActive: boolean;
}

export interface AdminOrder {
  id: string;
  orderNumber: string;
  customerName: string;
  customerPhone: string;
  cakeName: string;
  quantity: number;
  cakeSize: string;
  preferredDate: string | null;
  customization: string | null;
  status: CustomerOrderStatus;
  createdUtc: string;
  updatedUtc: string;
}

export type CustomerOrderStatus =
  | 'Pending'
  | 'Confirmed'
  | 'Preparing'
  | 'Ready'
  | 'Completed'
  | 'Cancelled';

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/admin';
  private readonly tokenKey = 'sweet-panda-admin-token';

  login(email: string, password: string): Observable<{ accessToken: string; expiresUtc: string; email: string; role: string }> {
    return this.http.post<{ accessToken: string; expiresUtc: string; email: string; role: string }>(`${this.base}/auth/login`, { email, password });
  }

  saveToken(token: string): void { sessionStorage.setItem(this.tokenKey, token); }
  getToken(): string | null { return sessionStorage.getItem(this.tokenKey); }
  clearToken(): void { sessionStorage.removeItem(this.tokenKey); }
  get isLoggedIn(): boolean { return !!this.getToken(); }

  private get options(): { headers: HttpHeaders } {
    return { headers: new HttpHeaders({ Authorization: `Bearer ${this.getToken() ?? ''}` }) };
  }

  getProducts(): Observable<AdminProduct[]> { return this.http.get<AdminProduct[]>(`${this.base}/products`, this.options); }
  createProduct(body: ProductWrite): Observable<unknown> { return this.http.post(`${this.base}/products`, body, this.options); }
  updateProduct(id: string, body: ProductWrite): Observable<unknown> { return this.http.put(`${this.base}/products/${id}`, body, this.options); }
  getCategories(): Observable<AdminCategory[]> { return this.http.get<AdminCategory[]>(`${this.base}/categories`, this.options); }
  createCategory(body: CategoryWrite): Observable<AdminCategory> { return this.http.post<AdminCategory>(`${this.base}/categories`, body, this.options); }
  updateCategory(id: string, body: CategoryWrite): Observable<AdminCategory> { return this.http.put<AdminCategory>(`${this.base}/categories/${id}`, body, this.options); }

  getOrders(): Observable<AdminOrder[]> {
    return this.http.get<AdminOrder[]>(
      `${this.base}/orders`,
      this.options
    );
  }

  updateOrderStatus(
    id: string,
    status: CustomerOrderStatus
  ): Observable<unknown> {
    return this.http.put(
      `${this.base}/orders/${id}/status`,
      { status },
      this.options
    );
  }
}
