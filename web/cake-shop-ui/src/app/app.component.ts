import { Component, OnInit, inject, signal } from '@angular/core';
import { catchError, of } from 'rxjs';
import { CatalogApiService, CatalogProductVariantDto, ProductVariantDto } from './catalog-api.service';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { OrderApiService, CustomerOrderResponse } from './order-api.service';

interface CakeProduct {
  id?: string;
  name: string;
  description: string;
  price: string;
  tag?: string;
  image: string;
  category: string;
  variants: ProductVariantDto[];
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})

export class AppComponent implements OnInit {
  readonly isAdminRoute = signal(
    typeof window !== 'undefined' && window.location.pathname.startsWith('/admin')
  );
  private readonly catalogApi = inject(CatalogApiService);


  private readonly orderApi = inject(OrderApiService);

  readonly isSubmittingOrder = signal(false);
  readonly savedOrder = signal<CustomerOrderResponse | null>(null);

  readonly apiConnected = signal(false);
  readonly brandName = 'The Sweet Panda';
  // Replace with the shop's WhatsApp number in international format, digits only.
  private readonly WHATSAPP_NUMBER = '8456057904';

  activeCategory = 'All';
  readonly mobileMenuOpen = signal(false);

  readonly categories = signal(['All', 'Celebration', 'Chocolate', 'Signature']);

  today = new Date().toISOString().slice(0, 10);

  // Phase 4: Customer ordering workflow
  readonly orderFormOpen = signal(false);
  readonly selectedProduct = signal<CakeProduct | null>(null);

  orderQuantity = 1;
  orderSize = '1 kg';
  preferredDate = '';
  cakeMessage = '';
  customerName = '';
  customerPhone = '';
  orderError = '';


  readonly products = signal<CakeProduct[]>([]);

  selectedVariantId = '';

  get selectedVariant(): CatalogProductVariantDto | null {
    return this.selectedProduct()?.variants.find(
      variant => variant.id === this.selectedVariantId
    ) ?? null;
  }

  get selectedUnitPrice(): number {
    return this.selectedVariant?.price ?? 0;
  }

  get selectedOrderTotal(): number {
    return this.selectedUnitPrice * this.orderQuantity;
  }

  ngOnInit(): void {
    this.catalogApi.getProducts().pipe(catchError(() => of(null))).subscribe((items) => {
      if (!items) return;
      this.apiConnected.set(true);
      this.products.set(items.map(item => {
        const variants = item.variants ?? [];
        const availablePrices = variants
          .filter(v => v.isAvailable)
          .map(v => v.price);

        const startingPrice = availablePrices.length
          ? Math.min(...availablePrices)
          : item.price;

        return {
          id: item.id,
          name: item.name,
          description: item.description,
          price: `From ₹${startingPrice}`,
          tag: item.badge ?? undefined,
          image: item.imageUrl ?? '',
          category: this.toDisplayCategory(item.categoryName),
          variants
        };
      }));
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
    this.mobileMenuOpen.update(isOpen => !isOpen);
  }

  closeMobileMenu(): void {
    this.mobileMenuOpen.set(false);
  }

  onOrderCakeChange(productName: string): void {

    this.selectedProduct.set(
      this.products().find(product => product.name === productName) ?? null
    );
    this.selectedVariantId =
      this.selectedProduct()?.variants.find(v => v.isAvailable)?.id ?? '';

    this.orderSize = this.selectedProduct()?.variants.find(
      v => v.id === this.selectedVariantId
    )?.sizeLabel ?? '';

    this.orderError = '';
  }
  orderOnWhatsApp(productName?: string): void {
    this.selectedProduct.set(
      this.products().find(product => product.name === productName) ?? null
    );

    this.orderQuantity = 1;
    this.selectedVariantId = this.selectedProduct()?.variants[0]?.id ?? '';
    this.orderSize = this.selectedProduct()?.variants[0]?.sizeLabel ?? '1 kg';
    this.preferredDate = '';
    this.cakeMessage = '';
    this.customerName = '';
    this.customerPhone = '';
    this.orderError = '';

    this.orderFormOpen.set(true);

    this.savedOrder.set(null);
    this.isSubmittingOrder.set(false);
  }

  closeOrderForm(): void {
    this.orderFormOpen.set(false);
    this.orderError = '';
  }


  submitOrder(): void {
    if (this.isSubmittingOrder()) return;

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


    if (
      this.selectedProduct()?.variants.length === 0 && !this.selectedVariant
    ) {
      this.orderError = 'Please select an available cake size.';
      return;
    }

    this.orderError = '';
    this.isSubmittingOrder.set(true);

    this.orderApi.createOrder({
      productId: selectedProduct.id,
      variantId: this.selectedVariant?.id ?? null,
      customerName: this.customerName.trim(),
      customerPhone: this.customerPhone.trim(),
      quantity: this.orderQuantity,
      cakeSize: this.selectedVariant!.sizeLabel,
      preferredDate: this.preferredDate || null,
      customization: this.cakeMessage.trim() || null
    }).subscribe({
      next: (response) => {
        this.savedOrder.set(response);
        this.isSubmittingOrder.set(false);
      },
      error: (error) => {
        this.isSubmittingOrder.set(false);
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
      `Size: ${this.selectedVariant?.sizeLabel ?? this.orderSize}`,
      `Unit price: ${this.selectedUnitPrice}`,
      `Estimated total: ${this.selectedOrderTotal}`,
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
