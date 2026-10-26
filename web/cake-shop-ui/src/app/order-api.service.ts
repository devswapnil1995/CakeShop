
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

export interface CreateCustomerOrderRequest {
  productId: string;
  customerName: string;
  customerPhone: string;
  quantity: number;
  cakeSize: string;
  preferredDate: string | null;
  customization: string | null;
  variantId?: string | null;
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


export interface CreatePaymentResponse {
  paymentId: string; // Internal PaymentTransaction ID
  orderId: string;   // Razorpay order ID
  amount: number;    // Amount in paise
  currency: string;
  keyId: string;
}

export interface VerifyPaymentRequest {
  paymentTransactionId: string;
  razorpayPaymentId: string;
  razorpaySignature: string;
}

export interface VerifyPaymentResponse {
  verified: boolean;
  status: string;
}

export interface CheckoutOtpRequestResponse {
  message: string;
  testCode?: string;
  expiresInSeconds: number;
  retryAfterSeconds: number;
}

export interface CheckoutOtpVerifyResponse {
  verified: boolean;
  verificationToken: string;
  expiresInSeconds: number;
  message: string;
}

export interface CartCheckoutItemRequest {
  productId: string;
  variantId: string;
  quantity: number;
}

export interface CartCheckoutRequest {
  customerName: string;
  customerPhone: string;
  verificationToken: string;
  items: CartCheckoutItemRequest[];
  preferredDate: string | null;
  customization: string | null;
}

export interface PaymentAvailabilityResponse {
  available: boolean;
}

@Injectable({ providedIn: 'root' })
export class OrderApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/v1`;

  createOrder(
    request: CreateCustomerOrderRequest
  ): Observable<CustomerOrderResponse> {
    return this.http.post<CustomerOrderResponse>(
      `${this.baseUrl}/orders`,
      request
    );
  }

  requestCheckoutOtp(phone: string): Observable<CheckoutOtpRequestResponse> {
    return this.http.post<CheckoutOtpRequestResponse>(`${this.baseUrl}/checkout/otp/request`, { phone });
  }

  verifyCheckoutOtp(phone: string, code: string): Observable<CheckoutOtpVerifyResponse> {
    return this.http.post<CheckoutOtpVerifyResponse>(`${this.baseUrl}/checkout/otp/verify`, { phone, code });
  }

  createCartOrder(request: CartCheckoutRequest): Observable<CustomerOrderResponse & { totalAmount: number; items: Array<{ productName: string; sizeLabel: string; quantity: number; unitPrice: number; lineTotal: number }> }> {
    return this.http.post<CustomerOrderResponse & { totalAmount: number; items: Array<{ productName: string; sizeLabel: string; quantity: number; unitPrice: number; lineTotal: number }> }>(
      `${this.baseUrl}/checkout/orders`,
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

  createPayment(orderId: string) {
    return this.http.post<CreatePaymentResponse>(
      `${this.baseUrl}/orders/${orderId}/payment`,
      {}
    );
  }

  verifyPayment(request: VerifyPaymentRequest) {
    return this.http.post<VerifyPaymentResponse>(
      `${this.baseUrl}/payments/verify`,
      request
    );
  }


  getPaymentAvailability(): Observable<PaymentAvailabilityResponse> {
    return this.http.get<PaymentAvailabilityResponse>(
      `${this.baseUrl}/payments/availability`
    );
  }
}
