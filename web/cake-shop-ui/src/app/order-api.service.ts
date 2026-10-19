
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface CreateCustomerOrderRequest {
  productId: string;
  customerName: string;
  customerPhone: string;
  quantity: number;
  cakeSize: string;
  preferredDate: string | null;
  customization: string | null;
}

export interface CustomerOrderResponse {
  id: string;
  orderNumber: string;
  cakeName: string;
  quantity: number;
  status: string;
  createdUtc: string;
  message: string;
}

export type CustomerOrderStatus =
  | 'Pending'
  | 'Confirmed'
  | 'Preparing'
  | 'Ready'
  | 'Completed'
  | 'Cancelled';

@Injectable({ providedIn: 'root' })
export class OrderApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1';

  createOrder(
    request: CreateCustomerOrderRequest
  ): Observable<CustomerOrderResponse> {
    return this.http.post<CustomerOrderResponse>(
      `${this.baseUrl}/orders`,
      request
    );
  }

  getAdminOrders(): Observable<CustomerOrderResponse[]> {
    return this.http.get<CustomerOrderResponse[]>(
      `${this.baseUrl}/admin/orders`
    );
  }

  updateOrderStatus(
    id: string,
    status: CustomerOrderStatus
  ): Observable<unknown> {
    return this.http.put(
      `${this.baseUrl}/admin/orders/${id}/status`,
      { status }
    );
  }
}
