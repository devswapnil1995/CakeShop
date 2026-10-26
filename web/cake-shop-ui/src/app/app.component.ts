import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { catchError, finalize, of } from 'rxjs';
import { CatalogApiService, CatalogProductVariantDto, ProductVariantDto, HeroBannerDto } from './catalog-api.service';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { environment } from '../environments/environment';
import { OrderApiService, CustomerOrderResponse, CreatePaymentResponse, VerifyPaymentRequest } from './order-api.service';

interface CartEntry {
  productId: string;
  variantId: string;
  quantity: number;
}

interface CartLine {
  key: string;
  product: CakeProduct;
  variant: ProductVariantDto;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

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

  readonly cartEntries = signal<CartEntry[]>([]);
  readonly cartDrawerOpen = signal(false);
  readonly addToCartOpen = signal(false);
  readonly addToCartProduct = signal<CakeProduct | null>(null);
  addToCartVariantId = '';
  addToCartQuantity = 1;
  readonly cartNotice = signal('');
  readonly checkoutOpen = signal(false);
  readonly checkoutSuccess = signal(false);
  readonly checkoutOtpVerified = signal(false);
  readonly checkoutTestOtp = signal('');
  readonly checkoutOtpMessage = signal('');
  readonly checkoutError = signal('');
  readonly checkoutVerificationToken = signal('');
  readonly isRequestingCheckoutOtp = signal(false);
  readonly isVerifyingCheckoutOtp = signal(false);
  readonly isSubmittingCartOrder = signal(false);
  readonly completedCartLines = signal<CartLine[]>([]);
  checkoutCustomerName = '';
  checkoutPhone = '';
  checkoutOtp = '';
  checkoutPreferredDate = '';
  checkoutCustomization = '';

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
  readonly legacyOtpVerified = signal(false);
  readonly legacyOtpMessage = signal('');
  readonly legacyTestOtp = signal('');
  readonly legacyVerificationToken = signal('');
  legacyOtp = '';
  readonly isRequestingLegacyOtp = signal(false);
  readonly isVerifyingLegacyOtp = signal(false);


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
      this.restoreCart();
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

  get cartLines(): CartLine[] {
    return this.cartEntries().flatMap(entry => {
      const product = this.products().find(item => item.id === entry.productId);
      const variant = product?.variants.find(item => item.id === entry.variantId && item.isAvailable);
      if (!product || !variant) return [];
      return [{
        key: `${entry.productId}:${entry.variantId}`,
        product,
        variant,
        quantity: entry.quantity,
        unitPrice: variant.price,
        lineTotal: Math.round(variant.price * entry.quantity * 100) / 100
      }];
    });
  }

  get checkoutPhoneDigits(): string {
    return this.checkoutPhone.replace(/\D/g, '');
  }

  get savedOrderTotal(): number {
    const order = this.savedOrder() as (CustomerOrderResponse & { totalAmount?: number }) | null;
    return order?.totalAmount ?? this.completedCartLines().reduce((sum, line) => sum + line.lineTotal, 0);
  }

  get cartSubtotal(): number {
    return Math.round(this.cartLines.reduce((total, line) => total + line.lineTotal, 0) * 100) / 100;
  }

  get cartItemCount(): number {
    return this.cartEntries().reduce((total, item) => total + item.quantity, 0);
  }

  formatPrice(amount: number): string {
    return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(amount);
  }

  beginAddToCart(productName: string): void {
    const product = this.products().find(item => item.name === productName);
    if (!product?.id) {
      this.cartNotice.set('This product is not available right now. Please refresh and try again.');
      return;
    }
    const availableVariants = product.variants.filter(item => item.isAvailable);
    if (!availableVariants.length) {
      this.cartNotice.set('Please contact us to confirm sizes and pricing for this cake.');
      return;
    }
    this.addToCartProduct.set(product);
    this.addToCartVariantId = availableVariants[0].id;
    this.addToCartQuantity = 1;
    this.addToCartOpen.set(true);
    this.cartNotice.set('');
  }

  confirmAddToCart(): void {
    const product = this.addToCartProduct();
    const variantId = this.addToCartVariantId;
    const variant = product?.variants.find(item => item.id === variantId && item.isAvailable);
    if (!product?.id || !variant) {
      this.cartNotice.set('Please choose an available size before adding this cake.');
      return;
    }
    if (!Number.isInteger(this.addToCartQuantity) || this.addToCartQuantity < 1 || this.addToCartQuantity > 100) {
      this.cartNotice.set('Choose a quantity between 1 and 100.');
      return;
    }
    const key = `${product.id}:${variant.id}`;
    const existing = this.cartEntries().find(item => `${item.productId}:${item.variantId}` === key);
    const nextQuantity = (existing?.quantity ?? 0) + this.addToCartQuantity;
    if (nextQuantity > 100) {
      this.cartNotice.set('A maximum of 100 of the same size can be added.');
      return;
    }
    this.cartEntries.update(entries => existing
      ? entries.map(item => `${item.productId}:${item.variantId}` === key ? { ...item, quantity: nextQuantity } : item)
      : [...entries, { productId: product.id!, variantId: variant.id, quantity: this.addToCartQuantity }]);
    this.persistCart();
    this.addToCartOpen.set(false);
    this.cartNotice.set(`${product.name} added to your bag.`);
  }

  closeAddToCart(): void {
    this.addToCartOpen.set(false);
    this.addToCartProduct.set(null);
  }

  clearCartNotice(): void { this.cartNotice.set(''); }

  openCart(): void {
    this.cartDrawerOpen.set(true);
    this.cartNotice.set('');
  }

  closeCart(): void {
    this.cartDrawerOpen.set(false);
  }

  updateCartQuantity(key: string, delta: number): void {
    this.cartEntries.update(entries => entries.flatMap(item => {
      if (`${item.productId}:${item.variantId}` !== key) return [item];
      const quantity = item.quantity + delta;
      return quantity < 1 ? [] : [{ ...item, quantity: Math.min(100, quantity) }];
    }));
    this.persistCart();
  }

  removeCartLine(key: string): void {
    this.cartEntries.update(entries => entries.filter(item => `${item.productId}:${item.variantId}` !== key));
    this.persistCart();
  }

  private persistCart(): void {
    try {
      if (typeof window !== 'undefined') {
        window.localStorage.setItem('sweet-panda-cart-v1', JSON.stringify(this.cartEntries()));
      }
    } catch {
      // The cart remains usable for this session if browser storage is unavailable.
    }
  }

  private restoreCart(): void {
    try {
      if (typeof window === 'undefined') return;
      const raw = window.localStorage.getItem('sweet-panda-cart-v1');
      if (!raw) return;
      const parsed: unknown = JSON.parse(raw);
      if (!Array.isArray(parsed)) return;
      const valid = parsed.filter((item): item is CartEntry =>
        !!item && typeof item.productId === 'string' &&
        typeof item.variantId === 'string' &&
        Number.isInteger(item.quantity) && item.quantity >= 1 && item.quantity <= 100)
        .filter(item => {
          const product = this.products().find(candidate => candidate.id === item.productId);
          return !!product?.variants.some(variant => variant.id === item.variantId && variant.isAvailable);
        });
      this.cartEntries.set(valid);
      // Product availability and variant prices are always resolved from the fresh API response.
      this.persistCart();
    } catch {
      this.cartEntries.set([]);
    }
  }

  startCartCheckout(): void {
    if (!this.cartLines.length) {
      this.cartNotice.set('Add at least one available cake to your bag before checkout.');
      return;
    }
    this.cartDrawerOpen.set(false);
    this.checkoutOpen.set(true);
    this.checkoutSuccess.set(false);
    this.checkoutOtpVerified.set(false);
    this.checkoutTestOtp.set('');
    this.checkoutOtpMessage.set('');
    this.checkoutError.set('');
    this.checkoutVerificationToken.set('');
    this.checkoutOtp = '';
    this.checkoutPreferredDate = '';
    this.checkoutCustomization = '';
    this.savedOrder.set(null);
    this.paymentMessage.set('');
    this.paymentError.set('');
  }

  startCartCheckout(): void {
    if (!this.cartLines.length) {
      this.cartNotice.set('Add at least one available cake to your bag before checkout.');
      return;
    }
    this.cartDrawerOpen.set(false);
    this.checkoutOpen.set(true);
    this.checkoutSuccess.set(false);
    this.checkoutOtpVerified.set(false);
    this.checkoutTestOtp.set('');
    this.checkoutOtpMessage.set('');
    this.checkoutError.set('');
    this.checkoutVerificationToken.set('');
    this.checkoutOtp = '';
    this.checkoutPreferredDate = '';
    this.checkoutCustomization = '';
    this.savedOrder.set(null);
    this.paymentMessage.set('');
    this.paymentError.set('');
  }

  closeCheckout(): void {
    this.checkoutOpen.set(false);
    this.checkoutError.set('');
  }

  resetCheckoutVerification(): void {
    this.checkoutOtpVerified.set(false);
    this.checkoutVerificationToken.set('');
    this.checkoutTestOtp.set('');
    this.checkoutOtpMessage.set('');
  }

  requestCheckoutOtp(): void {
    const phone = this.checkoutPhone.replace(/\D/g, '');
    if (phone.length < 10 || phone.length > 15) {
      this.checkoutError.set('Enter a valid mobile number with 10 to 15 digits.');
      return;
    }
    if (this.isRequestingCheckoutOtp()) return;
    this.checkoutError.set('');
    this.checkoutOtpMessage.set('');
    this.checkoutTestOtp.set('');
    this.isRequestingCheckoutOtp.set(true);
    this.orderApi.requestCheckoutOtp(phone).pipe(finalize(() => this.isRequestingCheckoutOtp.set(false))).subscribe({
      next: response => {
        this.checkoutOtpMessage.set(response.message + (response.retryAfterSeconds ? ` You can request another code in ${response.retryAfterSeconds}s.` : ''));
        this.checkoutTestOtp.set(response.testCode ?? '');
        this.checkoutOtpVerified.set(false);
        this.checkoutVerificationToken.set('');
      },
      error: error => this.checkoutError.set(error?.error?.detail ?? error?.error?.message ?? 'We could not request a verification code. Please try again.')
    });
  }

  verifyCheckoutOtp(): void {
    const phone = this.checkoutPhone.replace(/\D/g, '');
    if (!/^[0-9]{6}$/.test(this.checkoutOtp)) {
      this.checkoutError.set('Enter the 6-digit OTP.');
      return;
    }
    if (this.isVerifyingCheckoutOtp()) return;
    this.checkoutError.set('');
    this.isVerifyingCheckoutOtp.set(true);
    this.orderApi.verifyCheckoutOtp(phone, this.checkoutOtp).pipe(finalize(() => this.isVerifyingCheckoutOtp.set(false))).subscribe({
      next: response => {
        this.checkoutOtpVerified.set(response.verified);
        this.checkoutVerificationToken.set(response.verificationToken);
        this.checkoutOtpMessage.set(response.message);
        this.checkoutTestOtp.set('');
        this.checkoutError.set('');
      },
      error: error => {
        this.checkoutOtpVerified.set(false);
        this.checkoutVerificationToken.set('');
        this.checkoutError.set(error?.error?.message ?? 'OTP verification failed. Request a new code if it has expired.');
      }
    });
  }

  placeCartOrder(): void {
    if (!this.checkoutOtpVerified() || !this.checkoutVerificationToken()) {
      this.checkoutError.set('Verify your mobile number before placing the order.');
      return;
    }
    if (!this.checkoutCustomerName.trim() || this.checkoutCustomerName.trim().length > 120) {
      this.checkoutError.set('Enter your name (maximum 120 characters).');
      return;
    }
    const lines = this.cartLines;
    if (!lines.length) {
      this.checkoutError.set('Your cart no longer contains available items. Please review it again.');
      return;
    }
    if (this.isSubmittingCartOrder()) return;
    this.checkoutError.set('');
    this.isSubmittingCartOrder.set(true);
    this.completedCartLines.set(lines);
    this.orderApi.createCartOrder({
      customerName: this.checkoutCustomerName.trim(),
      customerPhone: this.checkoutPhone.replace(/\D/g, ''),
      verificationToken: this.checkoutVerificationToken(),
      items: lines.map(line => ({
        productId: line.product.id!,
        variantId: line.variant.id,
        quantity: line.quantity
      })),
      preferredDate: this.checkoutPreferredDate || null,
      customization: this.checkoutCustomization.trim() || null
    }).pipe(finalize(() => this.isSubmittingCartOrder.set(false))).subscribe({
      next: response => {
        this.savedOrder.set(response);
        this.checkoutSuccess.set(true);
        this.cartEntries.set([]);
        this.persistCart();
        this.checkoutVerificationToken.set('');
        this.checkoutOtpVerified.set(false);
      },
      error: error => {
        this.checkoutError.set(error?.error?.message ?? 'We could not place your order. Please review your cart and try again.');
        if (error?.status === 400 && String(error?.error?.message ?? '').toLowerCase().includes('verification')) {
          this.checkoutOtpVerified.set(false);
          this.checkoutVerificationToken.set('');
        }
      }
    });
  }

  continueCartToWhatsApp(): void {
    const order = this.savedOrder();
    if (!order) return;
    const items = this.completedCartLines().map(line =>
      `• ${line.product.name} — ${line.variant.sizeLabel} × ${line.quantity} = ${this.formatPrice(line.lineTotal)}`);
    const total = (order as CustomerOrderResponse & { totalAmount?: number }).totalAmount ?? this.completedCartLines().reduce((sum, line) => sum + line.lineTotal, 0);
    const message = [
      `Hi ${this.brandName}! I've submitted my cart order.`,
      `Order number: ${order.orderNumber}`,
      ...items,
      `Order total: ${this.formatPrice(total)}`,
      `Customer: ${this.checkoutCustomerName.trim()}`,
      `Verified mobile: ${this.checkoutPhone.replace(/\D/g, '')}`,
      ...(this.checkoutPreferredDate ? [`Preferred date: ${this.checkoutPreferredDate}`] : []),
      ...(this.checkoutCustomization.trim() ? [`Customisation: ${this.checkoutCustomization.trim()}`] : []),
      'Please confirm availability and any delivery charges.'
    ].join('\n');
    window.open(`https://wa.me/${this.WHATSAPP_NUMBER}?text=${encodeURIComponent(message)}`, '_blank', 'noopener,noreferrer');
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
    this.resetLegacyVerification();
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


  resetLegacyVerification(): void {
    this.legacyOtpVerified.set(false);
    this.legacyOtpMessage.set('');
    this.legacyTestOtp.set('');
    this.legacyVerificationToken.set('');
    this.legacyOtp = '';
  }

  requestLegacyOtp(): void {
    const phone = this.customerPhone.replace(/\D/g, '');
    if (phone.length !== 10) {
      this.orderError = 'Enter a valid 10-digit mobile number.';
      return;
    }
    if (this.isRequestingLegacyOtp()) return;
    this.orderError = '';
    this.isRequestingLegacyOtp.set(true);
    this.orderApi.requestCheckoutOtp(phone).pipe(finalize(() => this.isRequestingLegacyOtp.set(false))).subscribe({
      next: response => {
        this.legacyOtpMessage.set(response.message);
        this.legacyTestOtp.set(response.testCode ?? '');
        this.legacyOtpVerified.set(false);
        this.legacyVerificationToken.set('');
      },
      error: error => this.orderError = error?.error?.detail ?? error?.error?.message ?? 'Could not request an OTP.'
    });
  }

  verifyLegacyOtp(): void {
    const phone = this.customerPhone.replace(/\D/g, '');
    if (!/^[0-9]{6}$/.test(this.legacyOtp)) {
      this.orderError = 'Enter the 6-digit OTP.';
      return;
    }
    if (this.isVerifyingLegacyOtp()) return;
    this.orderError = '';
    this.isVerifyingLegacyOtp.set(true);
    this.orderApi.verifyCheckoutOtp(phone, this.legacyOtp).pipe(finalize(() => this.isVerifyingLegacyOtp.set(false))).subscribe({
      next: response => {
        this.legacyOtpVerified.set(response.verified);
        this.legacyVerificationToken.set(response.verificationToken);
        this.legacyOtpMessage.set(response.message);
        this.legacyTestOtp.set('');
        this.orderError = '';
      },
      error: error => {
        this.legacyOtpVerified.set(false);
        this.legacyVerificationToken.set('');
        this.orderError = error?.error?.message ?? 'OTP verification failed. Request a new code if it has expired.';
      }
    });
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

    if (!this.legacyOtpVerified() || !this.legacyVerificationToken()) {
      this.orderError = 'Verify your mobile number with OTP before submitting the order.';
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
      verificationToken: this.legacyVerificationToken(),
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
