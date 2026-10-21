import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
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
  unitPrice: number | null;
  totalAmount: number | null;
  preferredDate: string | null;
  customization: string | null;
  status: CustomerOrderStatus;
  createdUtc: string;
  updatedUtc: string;
}

export interface OrderStatusHistory {
  id: string;
  orderId: string;
  previousStatus: CustomerOrderStatus;
  newStatus: CustomerOrderStatus;
  changedUtc: string;
  changedBy: string | null;
}

export type CustomerOrderStatus =
  | 'Pending'
  | 'Confirmed'
  | 'Preparing'
  | 'Ready'
  | 'Completed'
  | 'Cancelled';

export interface DashboardOrder {
  id: string;
  orderNumber: string;
  customerName: string;
  cakeName: string;
  quantity: number;
  cakeSize: string;
  preferredDate: string | null;
  status: string;
  unitPrice: number | null;
  totalAmount: number | null;
}

export interface AdminDashboardSummary {
  localDate: string;
  createdToday: number;
  dueToday: number;
  pendingConfirmation: number;
  preparing: number;
  ready: number;
  completedRevenueToday: number | null;
  revenueAvailable: boolean;
  completedOrdersMissingPrice: number;
  dueOrders: DashboardOrder[];
  upcomingOrders: DashboardOrder[];
}


export interface AdminExpense {
  id: string;
  category: string;
  amount: number;
  expenseDate: string;
  description: string | null;
  createdUtc: string;
  updatedUtc: string;
}

export interface ExpensePage {
  items: AdminExpense[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ExpenseWrite {
  category: string;
  amount: number;
  expenseDate: string;
  description: string | null;
}


export interface ProfitReport {
  fromDate: string;
  toDate: string;
  completedOrderCount: number;
  completedOrdersMissingPrice: number;
  revenue: number;
  expenseCount: number;
  expenses: number;
  estimatedOperatingResult: number;
  revenueComplete: boolean;
  expenseBreakdown: ExpenseCategoryBreakdown[];
}

export interface ExpenseCategoryBreakdown {
  category: string;
  amount: number;
  count: number;
}


export interface AdminProductVariant {
  id: string;
  sizeLabel: string;
  weightInGrams: number;
  price: number;
  isAvailable: boolean;
}

export interface ProductVariantWrite {
  sizeLabel: string;
  weightInGrams: number;
  price: number;
  isAvailable: boolean;
}


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
  createProduct(body: ProductWrite): Observable<AdminProduct> {
    return this.http.post<AdminProduct>(
      `${this.base}/products`,
      body,
      this.options
    );
  }
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

  getDashboardSummary(): Observable<AdminDashboardSummary> { return this.http.get<AdminDashboardSummary>(`${this.base}/dashboard/summary`, this.options); }

  getOrderStatusHistory(
    id: string
  ): Observable<OrderStatusHistory[]> {
    return this.http.get<OrderStatusHistory[]>(
      `${this.base}/orders/${id}/history`,
      this.options
    );
  }


  getExpenses(
    page = 1,
    pageSize = 10,
    fromDate?: string,
    toDate?: string,
    category?: string
  ): Observable<ExpensePage> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    if (fromDate) params = params.set('fromDate', fromDate);
    if (toDate) params = params.set('toDate', toDate);
    if (category?.trim()) {
      params = params.set('category', category.trim());
    }

    return this.http.get<ExpensePage>(
      `${this.base}/expenses`,
      { ...this.options, params }
    );
  }

  getExpense(id: string): Observable<AdminExpense> {
    return this.http.get<AdminExpense>(
      `${this.base}/expenses/${id}`,
      this.options
    );
  }

  createExpense(body: ExpenseWrite): Observable<AdminExpense> {
    return this.http.post<AdminExpense>(
      `${this.base}/expenses`,
      body,
      this.options
    );
  }

  updateExpense(
    id: string,
    body: ExpenseWrite
  ): Observable<AdminExpense> {
    return this.http.put<AdminExpense>(
      `${this.base}/expenses/${id}`,
      body,
      this.options
    );
  }

  deleteExpense(id: string): Observable<void> {
    return this.http.delete<void>(
      `${this.base}/expenses/${id}`,
      this.options
    );
  }


  getProfitReport(fromDate: string, toDate: string): Observable<ProfitReport> {
    const params = new HttpParams()
      .set('fromDate', fromDate)
      .set('toDate', toDate);

    return this.http.get<ProfitReport>(
      `${this.base}/reports/profit`,
      { ...this.options, params }
    );
  }


  getProductVariants(productId: string): Observable<AdminProductVariant[]> {
    return this.http.get<AdminProductVariant[]>(
      `${this.base}/products/${productId}/variants`,
      this.options
    );
  }

  createProductVariant(
    productId: string,
    body: ProductVariantWrite
  ): Observable<AdminProductVariant> {
    return this.http.post<AdminProductVariant>(
      `${this.base}/products/${productId}/variants`,
      body,
      this.options
    );
  }

  updateProductVariant(
    productId: string,
    variantId: string,
    body: ProductVariantWrite
  ): Observable<AdminProductVariant> {
    return this.http.put<AdminProductVariant>(
      `${this.base}/products/${productId}/variants/${variantId}`,
      body,
      this.options
    );
  }

}
