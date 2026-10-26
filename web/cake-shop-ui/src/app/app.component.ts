import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { catchError, finalize, of } from 'rxjs';
import { CatalogApiService, CatalogProductVariantDto, ProductVariantDto, HeroBannerDto } from './catalog-api.service';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { environment } from '../environments/environment';
import { OrderApiService, CustomerOrderResponse, CreatePaymentResponse, VerifyPaymentRequest } from './order-api.service';

interface CakeProduct {
  id?: string;
  name: string;
  description: string;
  price: string;
  tag?: string;
  image: string;
  category: string;
  featured: boolean;
  variants: ProductVariantDto[];
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})

export class AppComponent implements OnDestroy, OnInit {
  readonly isAdminRoute = signal(
    typeof window !== 'undefined' && window.location.pathname.startsWith('/admin')
  );
  readonly isShopRoute = signal(
    typeof window !== 'undefined' && window.location.pathname.replace(/\/+$/, '') === '/shop'
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
  readonly heroBanners = signal<HeroBannerDto[]>([]);
  readonly activeHeroBannerIndex = signal(0);
  private heroRotationTimer: ReturnType<typeof setInterval> | undefined;
  readonly heroRotationPaused = signal(false);

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

  readonly isStartingPayment = signal(false);
  readonly paymentMessage = signal('');
  readonly paymentError = signal('');
  readonly paymentAvailabilityChecked = signal(false);
  readonly paymentAvailable = signal(false);



  resolveHeroMediaUrl(url: string | null): string {
    if (!url) return '';
    if (!url.startsWith('/')) return url;
    return `${environment.apiBaseUrl || 'http://localhost:5080'}${url}`;
  }

  sanitizeHeroButtonUrl(url: string | null): string | null {
    const value = url?.trim();
    if (!value || value.includes('\\')) return null;

    if (value.startsWith('/') && !value.startsWith('//')) return value;

    try {
      const parsed = new URL(value);
      return parsed.protocol === 'https:' && !parsed.username && !parsed.password
        ? parsed.href
        : null;
    } catch {
      return null;
    }
  }

  changeHeroBanner(direction: number): void {
    const count = this.heroBanners().length;
    if (count < 2) return;
    this.activeHeroBannerIndex.update(index => (index + direction + count) % count);
    this.restartHeroRotation();
  }

  setHeroBanner(index: number): void {
    if (index < 0 || index >= this.heroBanners().length) return;
    this.activeHeroBannerIndex.set(index);
    this.restartHeroRotation();
  }

  pauseHeroRotation(): void {
    this.heroRotationPaused.set(true);
    this.clearHeroRotation();
  }

  resumeHeroRotation(): void {
    this.heroRotationPaused.set(false);
    this.startHeroRotation();
  }

  private startHeroRotation(): void {
    this.clearHeroRotation();
    if (this.heroRotationPaused() || this.heroBanners().length < 2) return;
    this.heroRotationTimer = setInterval(() => {
      this.activeHeroBannerIndex.update(index => (index + 1) % this.heroBanners().length);
    }, 6000);
  }

  private restartHeroRotation(): void {
    if (!this.heroRotationPaused()) this.startHeroRotation();
  }

  private clearHeroRotation(): void {
    if (this.heroRotationTimer) clearInterval(this.heroRotationTimer);
    this.heroRotationTimer = undefined;
  }

  ngOnDestroy(): void {
    this.clearHeroRotation();
  }

  ngOnInit(): void {
    this.catalogApi.getHeroBanners().pipe(catchError(() => of([] as HeroBannerDto[]))).subscribe(banners => {
      this.heroBanners.set(banners);
      this.activeHeroBannerIndex.set(0);
      this.startHeroRotation();
    });
    this.orderApi.getPaymentAvailability().subscribe({
      next: (response) => {
        this.paymentAvailable.set(response.available);
        this.paymentAvailabilityChecked.set(true);
      },
      error: () => {
        // Fail closed: never offer online checkout if availability is unknown.
        this.paymentAvailable.set(false);
        this.paymentAvailabilityChecked.set(true);
      }
    });
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
          price: `From ${new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(startingPrice)}`,
          tag: item.badge ?? undefined,
          image: this.resolveProductImageUrl(item.imageUrl),
          category: this.toDisplayCategory(item.categoryName),
          featured: item.isFeatured,
          variants
        };
      }));
      const categoryNames = [...new Set(items.map((item) => this.toDisplayCategory(item.categoryName)))];
      this.categories.set(['All', ...categoryNames]);
    });
  }

  resolveProductImageUrl(url: string | null): string {
    if (!url) return 'https://images.unsplash.com/photo-1578985545062-69928b1d9587?auto=format&fit=crop&w=900&q=80';
    if (url.startsWith('/')) return `${environment.apiBaseUrl || 'http://localhost:5080'}${url}`;
    return url;
  }

  onProductImageError(event: Event): void {
    const image = event.target as HTMLImageElement;
    image.onerror = null;
    image.src = 'https://images.unsplash.com/photo-1578985545062-69928b1d9587?auto=format&fit=crop&w=900&q=80';
  }

  private toDisplayCategory(name: string): string {
    return name;
  }

  get homeProducts(): CakeProduct[] {
    return this.products().filter(product =>
      product.featured || Boolean(product.tag?.trim())
    );
  }

  get visibleProducts(): CakeProduct[] {
    const catalogue = this.isShopRoute() ? this.products() : this.homeProducts;
    return this.activeCategory === 'All'
      ? catalogue
      : catalogue.filter(product => product.category === this.activeCategory);
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
  startProductOrder(productName: string): void {
    const product = this.products().find(item => item.name === productName);
    if (product && product.variants.length === 0) {
      const message = `Hi ${this.brandName}! I'm interested in ${product.name}. Could you please share the available sizes and current pricing?`;
      const url = `https://wa.me/${this.WHATSAPP_NUMBER}?text=${encodeURIComponent(message)}`;
      window.open(url, '_blank', 'noopener,noreferrer');
      return;
    }
    this.orderOnWhatsApp(productName);
  }

  orderOnWhatsApp(productName?: string): void {
    this.selectedProduct.set(
      this.products().find(product => product.name === productName) ?? null
    );

    this.orderQuantity = 1;
    this.selectedVariantId = this.selectedProduct()?.variants.find(v => v.isAvailable)?.id ?? '';
    this.orderSize = this.selectedProduct()?.variants.find(v => v.isAvailable)?.sizeLabel ?? '';
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
    this.paymentMessage.set('');
  }


  submitOrder(): void {
    if (this.isSubmittingOrder()) return;

    this.paymentMessage.set('');

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


  startOnlinePayment(): void {
    const order = this.savedOrder();

    if (!order || this.isStartingPayment()) return;

    this.paymentMessage.set('');
    this.paymentError.set('');
    this.isStartingPayment.set(true);

    this.orderApi.createPayment(order.id)
      .pipe(finalize(() => this.isStartingPayment.set(false)))
      .subscribe({
        next: (payment: CreatePaymentResponse) => {
          this.openRazorpayCheckout(payment);
        },
        error: (error) => {
          this.paymentError.set(
            error?.error?.message ??
            'Online payments are not configured yet. Please continue using WhatsApp.'
          );
        }
      });
  }

  private openRazorpayCheckout(payment: CreatePaymentResponse): void {
    const razorpayWindow = window as Window & {
      Razorpay?: new (options: Record<string, unknown>) => {
        open: () => void;
      };
    };

    if (!razorpayWindow.Razorpay) {
      this.paymentError.set(
        'Payment checkout could not be loaded. Please use WhatsApp instead.'
      );
      return;
    }

    const checkout = new razorpayWindow.Razorpay({
      key: payment.keyId,
      amount: payment.amount,
      currency: payment.currency,
      name: 'The Sweet Panda',
      description: 'Cake order payment',
      order_id: payment.orderId,
      handler: (response: {
        razorpay_payment_id: string;
        razorpay_order_id: string;
        razorpay_signature: string;
      }) => {
        // Verify against the server-created payment transaction.
        this.orderApi.verifyPayment({
          paymentTransactionId: payment.paymentId,
          razorpayPaymentId: response.razorpay_payment_id,
          razorpaySignature: response.razorpay_signature
        } satisfies VerifyPaymentRequest).subscribe({
          next: result => {
            if (result.verified) {
              this.paymentMessage.set('Payment verified successfully.');
            } else {
              this.paymentError.set(
                'Payment verification is pending. Please contact the shop before paying again.'
              );
            }
          },
          error: () => {
            this.paymentError.set(
              'We could not verify the payment yet. Please contact the shop before retrying.'
            );
          }
        });
      },
      modal: {
        ondismiss: () => {
          this.paymentMessage.set('Checkout closed. You can still continue on WhatsApp.');
        }
      },
      theme: { color: '#3e2b24' }
    });

    checkout.open();
  }
}
