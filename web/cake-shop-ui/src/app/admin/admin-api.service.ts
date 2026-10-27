import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';


export interface AdminAboutSection {
  id: string; eyebrow: string; title: string; body: string; imageUrl: string | null; secondaryImageUrl: string | null;
  layout: 'image-left' | 'image-right' | 'centered' | 'full-bleed' | 'editorial';
  animation: 'none' | 'fade-up' | 'fade-in' | 'slide-left' | 'slide-right' | 'zoom-in';
  sortOrder: number; isPublished: boolean;
}
export type AboutSectionWrite = Omit<AdminAboutSection, 'id'>;

export interface AdminStoreProfile {
  clientId: string; name: string; slug: string; tagline: string; storyTitle: string; story: string;
  storyImageUrl: string | null; logoUrl: string | null; phone: string | null; whatsAppNumber: string | null;
  email: string | null; address: string | null; openingHours: string | null; instagramUrl: string | null;
  facebookUrl: string | null; websiteUrl: string | null;
}
export type StoreProfileWrite = Omit<AdminStoreProfile, 'clientId' | 'name' | 'slug'>;

export interface AdminHeroBanner {
  id: string;
  title: string;
  subtitle: string;
  textPosition: HeroBannerTextPosition;
  mediaType: 'Image' | 'Video';
  mediaUrl: string;
  mobileMediaUrl: string | null;
  buttonText: string | null;
  buttonUrl: string | null;
  sortOrder: number;
  startsUtc: string | null;
  endsUtc: string | null;
  isPublished: boolean;
}

export type HeroBannerTextPosition = 'top' | 'bottom' | 'left' | 'right';

export interface HeroBannerWrite {
  title: string;
  subtitle: string;
  textPosition: HeroBannerTextPosition;
  mediaType: 'Image' | 'Video';
  mediaUrl: string;
  mobileMediaUrl: string | null;
  buttonText: string | null;
  buttonUrl: string | null;
  sortOrder: number;
  startsUtc: string | null;
  endsUtc: string | null;
}

export interface AdminStoreCollection {
  id: string; kind: 'Hamper' | 'Seasonal'; title: string; slug: string; description: string;
  imageUrl: string | null; navigationLabel: string | null; showInNavigation: boolean; bundlePrice: number | null;
  isPublished: boolean; highlights: string; sortOrder: number; startsUtc: string | null; endsUtc: string | null;
}
export interface StoreCollectionWrite {
  kind: 'Hamper' | 'Seasonal'; title: string; slug: string; description: string;
  imageUrl: string | null; navigationLabel: string | null; showInNavigation: boolean; bundlePrice: number | null;
  sortOrder: number; startsUtc: string | null; endsUtc: string | null; isPublished: boolean; highlights: string;
}

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
  paymentStatus: string | null;
  items?: Array<{
    productName: string;
    sizeLabel: string;
    weightInGrams: number;
    quantity: number;
    unitPrice: number;
    lineTotal: number;
  }>;
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
  private readonly base = `${environment.apiBaseUrl}/api/v1/admin`;
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
  uploadProductImage(file: File): Observable<{ mediaUrl: string }> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<{ mediaUrl: string }>(`${this.base}/products/images`, form, this.options);
  }
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

  getStoreCollections(): Observable<AdminStoreCollection[]> {
    return this.http.get<AdminStoreCollection[]>(`${this.base}/collections`, this.options);
  }
  createStoreCollection(body: StoreCollectionWrite): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/collections`, body, this.options);
  }
  updateStoreCollection(id: string, body: StoreCollectionWrite): Observable<void> {
    return this.http.put<void>(`${this.base}/collections/${id}`, body, this.options);
  }
  publishStoreCollection(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/collections/${id}/publish`, {}, this.options);
  }
  unpublishStoreCollection(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/collections/${id}/unpublish`, {}, this.options);
  }
  deleteStoreCollection(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/collections/${id}`, this.options);
  }

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



  uploadHeroBannerMedia(file: File): Observable<{ mediaUrl: string; mediaType: 'Image' | 'Video' }> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<{ mediaUrl: string; mediaType: 'Image' | 'Video' }>(
      `${this.base}/hero-banners/media`, body, this.options
    );
  }

  getAboutSections(): Observable<AdminAboutSection[]> {
    return this.http.get<AdminAboutSection[]>(`${this.base}/about-sections`, this.options);
  }
  createAboutSection(body: AboutSectionWrite): Observable<AdminAboutSection> {
    return this.http.post<AdminAboutSection>(`${this.base}/about-sections`, body, this.options);
  }
  updateAboutSection(id: string, body: AboutSectionWrite): Observable<void> {
    return this.http.put<void>(`${this.base}/about-sections/${id}`, body, this.options);
  }
  deleteAboutSection(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/about-sections/${id}`, this.options);
  }

  getStoreProfile(): Observable<AdminStoreProfile> {
    return this.http.get<AdminStoreProfile>(`${this.base}/store-profile`, this.options);
  }

  updateStoreProfile(body: StoreProfileWrite): Observable<void> {
    return this.http.put<void>(`${this.base}/store-profile`, body, this.options);
  }

  getHeroBanners(): Observable<AdminHeroBanner[]> {
    return this.http.get<AdminHeroBanner[]>(`${this.base}/hero-banners`, this.options);
  }

  createHeroBanner(body: HeroBannerWrite): Observable<AdminHeroBanner> {
    return this.http.post<AdminHeroBanner>(`${this.base}/hero-banners`, body, this.options);
  }

  updateHeroBanner(id: string, body: HeroBannerWrite): Observable<AdminHeroBanner> {
    return this.http.put<AdminHeroBanner>(`${this.base}/hero-banners/${id}`, body, this.options);
  }

  publishHeroBanner(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/hero-banners/${id}/publish`, {}, this.options);
  }

  unpublishHeroBanner(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/hero-banners/${id}/unpublish`, {}, this.options);
  }

  deleteHeroBanner(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/hero-banners/${id}`, this.options);
  }

}
