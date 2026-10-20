import { Component, OnInit, inject, signal } from '@angular/core';
import { catchError, of } from 'rxjs';
import { CatalogApiService } from './catalog-api.service';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { OrderApiService, CustomerOrderResponse } from './order-api.service';

interface CakeProduct {
  name: string;
  description: string;
  price: string;
  tag?: string;
  image: string;
  category: string;
}

interface CakeProduct {
  id?: string;
  name: string;
  description: string;
  price: string;
  tag?: string;
  image: string;
  category: string;
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})
export class AppComponent implements OnInit {
  readonly isAdminRoute = typeof window !== 'undefined' && window.location.pathname.startsWith('/admin');
  private readonly catalogApi = inject(CatalogApiService);


  private readonly orderApi = inject(OrderApiService);

  isSubmittingOrder = false;
  readonly savedOrder = signal<CustomerOrderResponse | null>(null);

  readonly apiConnected = signal(false);
  readonly brandName = 'The Sweet Panda';
  // Replace with the shop's WhatsApp number in international format, digits only.
  private readonly WHATSAPP_NUMBER = '8456057904';

  activeCategory = 'All';
  mobileMenuOpen = false;

  readonly categories = signal(['All', 'Celebration', 'Chocolate', 'Signature']);

  today = new Date().toISOString().slice(0, 10);

  // Phase 4: Customer ordering workflow
  orderFormOpen = false;
  readonly selectedProduct = signal<CakeProduct | null>(null);

  orderQuantity = 1;
  orderSize = '1 kg';
  preferredDate = '';
  cakeMessage = '';
  customerName = '';
  customerPhone = '';
  orderError = '';


  readonly products = signal<CakeProduct[]>([
    {
      name: 'Velvet Rose Cake',
      description: 'Silky vanilla cream, soft sponge and delicate rose finish.',
      price: 'From ₹799',
      tag: 'Bestseller',
      image: 'https://images.unsplash.com/photo-1578985545062-69928b1d9587?auto=format&fit=crop&w=900&q=85',
      category: 'Celebration',
    },
    {
      name: 'Midnight Chocolate',
      description: 'Deep cocoa layers with a glossy chocolate ganache.',
      price: 'From ₹899',
      tag: 'Chocolate lover’s pick',
      image: 'https://images.unsplash.com/photo-1606313564200-e75d5e30476c?auto=format&fit=crop&w=900&q=85',
      category: 'Chocolate',
    },
    {
      name: 'Berry Cloud Cake',
      description: 'Light whipped cream, seasonal berries and soft vanilla.',
      price: 'From ₹849',
      image: 'https://images.unsplash.com/photo-1565958011703-44f9829ba187?auto=format&fit=crop&w=900&q=85',
      category: 'Signature',
    },
  ]);

  ngOnInit(): void {
    this.catalogApi.getProducts().pipe(catchError(() => of(null))).subscribe((items) => {
      if (!items) return;
      this.apiConnected.set(true);
      this.products.set(items.map((item) => ({
        id: item.id,
        name: item.name,
        description: item.description,
        price: `From ₹${item.price}`,
        tag: item.badge ?? undefined,
        image: item.imageUrl ?? '',
        category: this.toDisplayCategory(item.categoryName)
      })));
      const categoryNames = [...new Set(items.map((item) => this.toDisplayCategory(item.categoryName)))];
      this.categories.set(['All', ...categoryNames]);
    });
  }

  private toDisplayCategory(name: string): string {
    return name;
  }

  get visibleProducts(): CakeProduct[] {
    return this.activeCategory === 'All'
      ? this.products()
      : this.products().filter((product) => product.category === this.activeCategory);
  }

  setCategory(category: string): void {
    this.activeCategory = category;
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen = !this.mobileMenuOpen;
  }

  onOrderCakeChange(productName: string): void {
    this.selectedProduct.set(
      this.products().find(product => product.name === productName) ?? null
    );

    this.orderError = '';
  }
  orderOnWhatsApp(productName?: string): void {
    this.selectedProduct.set(
      this.products().find(product => product.name === productName) ?? null
    );

    this.orderQuantity = 1;
    this.orderSize = '1 kg';
    this.preferredDate = '';
    this.cakeMessage = '';
    this.customerName = '';
    this.customerPhone = '';
    this.orderError = '';

    this.orderFormOpen = true;

    this.savedOrder.set(null);
    this.isSubmittingOrder = false;
  }

  closeOrderForm(): void {
    this.orderFormOpen = false;
    this.orderError = '';
  }


  submitOrder(): void {
    if (this.isSubmittingOrder) return;

    if (!this.customerName.trim()) {
      this.orderError = 'Please enter your name.';
      return;
    }

    if (!/^[0-9]{10}$/.test(this.customerPhone.trim())) {
      this.orderError = 'Please enter a valid 10-digit mobile number.';
      return;
    }

    if (this.orderQuantity < 1 || this.orderQuantity > 100) {
      this.orderError = 'Quantity must be between 1 and 100.';
      return;
    }

    const selectedProduct = this.selectedProduct();
    if (!selectedProduct?.id) {
      this.orderError =
        'The selected cake is not connected to the catalogue API. Please refresh and try again.';
      return;
    }

    this.orderError = '';
    this.isSubmittingOrder = true;

    this.orderApi.createOrder({
      productId: selectedProduct.id,
      customerName: this.customerName.trim(),
      customerPhone: this.customerPhone.trim(),
      quantity: this.orderQuantity,
      cakeSize: this.orderSize,
      preferredDate: this.preferredDate || null,
      customization: this.cakeMessage.trim() || null
    }).subscribe({
      next: (response) => {
        this.savedOrder.set(response);
        this.isSubmittingOrder = false;
      },
      error: (error) => {
        this.isSubmittingOrder = false;
        this.orderError =
          error?.error?.message ??
          'We could not save your order. Please try again.';
      }
    });
  }

  continueToWhatsApp(): void {
    const savedOrder = this.savedOrder();
    const selectedProduct = this.selectedProduct();
    if (!savedOrder || !selectedProduct) return;

    const message = [
      `Hi ${this.brandName}! I've submitted an order request.`,
      `Order number: ${savedOrder.orderNumber}`,
      `Cake: ${savedOrder.cakeName}`,
      `Quantity: ${savedOrder.quantity}`,
      `Size: ${this.orderSize}`,
      `Customer: ${this.customerName.trim()}`,
      `Mobile: ${this.customerPhone.trim()}`,
      ...(this.preferredDate
        ? [`Preferred date: ${this.preferredDate}`]
        : []),
      ...(this.cakeMessage.trim()
        ? [`Cake message/customization: ${this.cakeMessage.trim()}`]
        : []),
      'Please confirm availability and final price.'
    ].join('\n');

    const url =
      `https://wa.me/${this.WHATSAPP_NUMBER}?text=${encodeURIComponent(message)}`;

    window.open(url, '_blank', 'noopener,noreferrer');
  }


}
