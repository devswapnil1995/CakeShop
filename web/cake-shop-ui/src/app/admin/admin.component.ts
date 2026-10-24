import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AdminApiService, AdminCategory, AdminProduct, CategoryWrite, ProductWrite, AdminOrder,
  CustomerOrderStatus,
  AdminDashboardSummary, OrderStatusHistory,
  ExpenseWrite,
  AdminExpense,
  ExpensePage,
  ProfitReport,
  ExpenseCategoryBreakdown,
  AdminProductVariant,
  ProductVariantWrite,
  AdminHeroBanner,
  HeroBannerTextPosition,
  HeroBannerWrite
} from './admin-api.service';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin.component.html',
  styleUrl: './admin.component.css',
})
export class AdminComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  loggedIn = signal(false);
  readonly loading = signal(false);
  readonly saving = signal(false);
  activeTab:
    | 'dashboard'
    | 'products'
    | 'categories'
    | 'orders'
    | 'expenses'
    | 'reports'
    | 'hero-banners' = 'dashboard';

  readonly heroBanners = signal<AdminHeroBanner[]>([]);
  readonly heroBannersLoading = signal(false);
  readonly heroBannerSaving = signal(false);
  readonly heroMediaUploading = signal(false);
  editingHeroBannerId: string | null = null;
  heroBannerForm: HeroBannerWrite & { startsLocal: string; endsLocal: string } = this.emptyHeroBanner();

  private emptyHeroBanner(): HeroBannerWrite & { startsLocal: string; endsLocal: string } {
    return {
      title: '', subtitle: '', textPosition: 'left', mediaType: 'Image', mediaUrl: '', mobileMediaUrl: null,
      buttonText: null, buttonUrl: null, sortOrder: 0, startsUtc: null, endsUtc: null,
      startsLocal: '', endsLocal: ''
    };
  }

  readonly dashboard = signal<AdminDashboardSummary | null>(null);
  readonly dashboardLoading = signal(false);
  error = signal('');
  notice = '';
  email = '';
  password = '';
  readonly products = signal<AdminProduct[]>([]);
  readonly categories = signal<AdminCategory[]>([]);
  productSearch = '';
  productCategoryFilter = 'All';
  productAvailabilityFilter: 'All' | 'Available' | 'Unavailable' = 'All';
  readonly productImageUploading = signal(false);
  editingProductId: string | null = null;
  editingCategoryId: string | null = null;

  productForm: ProductWrite = this.emptyProduct();
  categoryForm: CategoryWrite = this.emptyCategory();

  readonly orders = signal<AdminOrder[]>([]);
  readonly loadingOrders = signal(false);
  updatingOrderIds = new Set<string>();

  orderSearch = '';
  orderStatusFilter: CustomerOrderStatus | 'All' = 'All';
  orderDateFilter = '';

  readonly selectedOrder = signal<AdminOrder | null>(null);

  readonly orderStatuses: CustomerOrderStatus[] = [
    'Pending',
    'Confirmed',
    'Preparing',
    'Ready',
    'Completed',
    'Cancelled'
  ];

  readonly orderStatusHistory = signal<OrderStatusHistory[]>([]);
  readonly historyLoading = signal(false);
  historyError = '';


  orderSort: 'newest' | 'oldest' | 'preferredDate' = 'newest';

  ordersPerPage = 10;
  currentOrderPage = 1;

  readonly orderPageSizes = [5, 10, 20, 50];

  readonly expenses = signal<AdminExpense[]>([]);
  readonly expensePage = signal<ExpensePage | null>(null);
  readonly loadingExpenses = signal(false);
  editingExpenseId: string | null = null;
  readonly expenseSaving = signal(false);
  deletingExpenseId: string | null = null;

  expensePageNumber = 1;
  expensePageSize = 10;
  expenseFromDate = '';
  expenseToDate = '';
  expenseCategoryFilter = '';

  readonly expensePageSizes = [5, 10, 20, 50];

  readonly expenseCategories = [
    'Ingredients',
    'Packaging',
    'Utilities',
    'Rent',
    'Equipment',
    'Delivery',
    'Marketing',
    'Maintenance',
    'Other'
  ];


  readonly profitReport = signal<ProfitReport | null>(null);
  readonly profitReportLoading = signal(false);
  profitFromDate = this.firstDayOfMonth();
  profitToDate = this.localToday();

  expenseForm: ExpenseWrite = this.emptyExpense();

  expenseBreakdown: ExpenseCategoryBreakdown[] = [];

  get featuredCount(): number { return this.products().filter(product => product.isFeatured).length; }

  productVariants: AdminProductVariant[] = [];
  readonly variantsLoading = signal(false);
  readonly variantSaving = signal(false);
  editingVariantId: string | null = null;

  variantForm: ProductVariantWrite = this.emptyVariant();

  private emptyVariant(): ProductVariantWrite {
    return {
      sizeLabel: '',
      weightInGrams: 500,
      price: 0,
      isAvailable: true
    };
  }

  readonly showVariantModal = signal(false);

  ngOnInit(): void {
    this.loggedIn.set(this.api.isLoggedIn);
    if (this.loggedIn()) {
      this.loadData();
      this.loadDashboard();
      this.loadOrders();
      this.loadHeroBanners();
    }
  }

  login(): void {
    this.error.set('');
    this.notice = '';
    this.loading.set(true);
    this.api.login(this.email.trim(), this.password).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: result => {
        this.api.saveToken(result.accessToken);
        this.loggedIn.set(true);
        this.password = '';
        this.loadData();
        this.loadDashboard();
        this.loadOrders();
        this.loadHeroBanners();
      },
      error: err => {
        this.error.set(err.status === 401 ? 'Email or password is incorrect.' : (err.error?.message ?? 'Unable to sign in. Check that the API is running and the admin account has been seeded.'));
      },
    });
  }

  logout(): void {
    this.api.clearToken();
    this.loggedIn.set(false);
    this.products.set([]);
    this.categories.set([]);
    this.orders.set([]);
    this.selectedOrder.set(null);
    this.resetProduct();
    this.error.set('');
    this.orderStatusHistory.set([]);
    this.historyLoading.set(false);
    this.historyError = '';

    this.expenses.set([]);
    this.expensePage.set(null);
    this.resetExpense();
  }

  loadData(): void {
    this.loading.set(true); this.error.set('');
    let completed = 0;
    const done = () => {
      completed++; if (completed === 2)
        this.loading.set(false);
    };

    this.api.getCategories().pipe(finalize(done)).subscribe({
      next: rows => this.categories.set(rows),
      error: err => this.handleApiError(err)
    });

    this.api.getProducts().pipe(finalize(done)).subscribe({
      next: rows => this.products.set(rows),
      error: err => this.handleApiError(err)
    });
  }

  selectTab(
    tab: 'dashboard' | 'products' | 'categories' | 'orders' | 'expenses' | 'reports' | 'hero-banners'
  ): void {
    this.activeTab = tab;
    this.error.set('');
    this.notice = '';

    if (tab === 'orders') {
      this.loadOrders();
    }

    if (tab === 'dashboard') {
      this.loadDashboard();
    }

    if (tab === 'expenses') {
      this.loadExpenses();
    }

    if (tab === 'reports') {
      this.loadProfitReport();
    }
    if (tab === 'hero-banners') {
      this.loadHeroBanners();
    }
  }


  resolveHeroMediaUrl(url: string | null): string {
    if (!url) return '';
    if (!url.startsWith('/')) return url;
    return `${environment.apiBaseUrl || 'http://localhost:5080'}${url}`;
  }

  loadHeroBanners(): void {
    if (!this.loggedIn()) return;
    this.heroBannersLoading.set(true);
    this.api.getHeroBanners().pipe(finalize(() => this.heroBannersLoading.set(false))).subscribe({
      next: rows => this.heroBanners.set(rows),
      error: err => this.handleApiError(err)
    });
  }

  readonly heroTextPositions: { value: HeroBannerTextPosition; label: string }[] = [
    { value: 'top', label: 'Top' },
    { value: 'bottom', label: 'Bottom' },
    { value: 'left', label: 'Left' },
    { value: 'right', label: 'Right' }
  ];

  resetHeroBannerForm(): void {
    this.editingHeroBannerId = null;
    this.heroBannerForm = this.emptyHeroBanner();
  }

  editHeroBanner(banner: AdminHeroBanner): void {
    this.editingHeroBannerId = banner.id;
    this.heroBannerForm = {
      title: banner.title,
      subtitle: banner.subtitle,
      textPosition: banner.textPosition || 'left',
      mediaType: banner.mediaType,
      mediaUrl: banner.mediaUrl,
      mobileMediaUrl: banner.mobileMediaUrl,
      buttonText: banner.buttonText,
      buttonUrl: banner.buttonUrl,
      sortOrder: banner.sortOrder,
      startsUtc: banner.startsUtc,
      endsUtc: banner.endsUtc,
      startsLocal: banner.startsUtc ? this.toLocalDateTimeInput(banner.startsUtc) : '',
      endsLocal: banner.endsUtc ? this.toLocalDateTimeInput(banner.endsUtc) : ''
    };
  }


  uploadHeroMedia(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.error.set('');
    this.heroMediaUploading.set(true);
    this.api.uploadHeroBannerMedia(file).pipe(finalize(() => {
      this.heroMediaUploading.set(false);
      input.value = '';
    })).subscribe({
      next: result => {
        this.heroBannerForm.mediaUrl = result.mediaUrl;
        this.heroBannerForm.mediaType = result.mediaType;
        this.notice = 'Media uploaded. Save the banner to keep this change.';
      },
      error: err => this.handleApiError(err)
    });
  }

  saveHeroBanner(): void {
    this.error.set('');
    this.notice = '';
    const form = this.heroBannerForm;
    const body: HeroBannerWrite = {
      title: form.title.trim(),
      subtitle: form.subtitle.trim(),
      textPosition: form.textPosition,
      mediaType: form.mediaType,
      mediaUrl: form.mediaUrl.trim(),
      mobileMediaUrl: form.mobileMediaUrl?.trim() || null,
      buttonText: form.buttonText?.trim() || null,
      buttonUrl: form.buttonUrl?.trim() || null,
      sortOrder: Number(form.sortOrder),
      startsUtc: form.startsLocal ? new Date(form.startsLocal).toISOString() : null,
      endsUtc: form.endsLocal ? new Date(form.endsLocal).toISOString() : null
    };
    this.heroBannerSaving.set(true);
    const request = this.editingHeroBannerId
      ? this.api.updateHeroBanner(this.editingHeroBannerId, body)
      : this.api.createHeroBanner(body);
    request.pipe(finalize(() => this.heroBannerSaving.set(false))).subscribe({
      next: () => {
        this.notice = this.editingHeroBannerId ? 'Hero banner updated.' : 'Hero banner created as draft.';
        this.resetHeroBannerForm();
        this.loadHeroBanners();
      },
      error: err => this.handleApiError(err)
    });
  }

  toggleHeroBannerPublication(banner: AdminHeroBanner): void {
    this.error.set('');
    const request = banner.isPublished
      ? this.api.unpublishHeroBanner(banner.id)
      : this.api.publishHeroBanner(banner.id);
    request.subscribe({
      next: () => {
        this.notice = banner.isPublished ? 'Hero banner unpublished.' : 'Hero banner published.';
        this.loadHeroBanners();
      },
      error: err => this.handleApiError(err)
    });
  }

  deleteHeroBanner(banner: AdminHeroBanner): void {
    if (!confirm(`Delete hero banner "${banner.title}"? This cannot be undone.`)) return;
    this.api.deleteHeroBanner(banner.id).subscribe({
      next: () => {
        this.notice = 'Hero banner deleted.';
        if (this.editingHeroBannerId === banner.id) this.resetHeroBannerForm();
        this.loadHeroBanners();
      },
      error: err => this.handleApiError(err)
    });
  }

  private toLocalDateTimeInput(value: string): string {
    const date = new Date(value);
    const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
    return local.toISOString().slice(0, 16);
  }

  loadOrders(): void {
    this.loadingOrders.set(true);
    this.error.set('');

    this.api.getOrders()
      .pipe(finalize(() => this.loadingOrders.set(false)))
      .subscribe({
        next: orders => {
          this.orders.set(orders);

          if (this.currentOrderPage > this.totalOrderPages) {
            this.currentOrderPage = this.totalOrderPages;
          }
        },
        error: err => this.handleApiError(err)
      });
  }


  loadDashboard(): void {
    if (!this.loggedIn()) return;

    this.dashboardLoading.set(true);

    this.api.getDashboardSummary()
      .pipe(finalize(() => {
        this.dashboardLoading.set(false);
      }))
      .subscribe({
        next: (result) => {
          this.dashboard.set(result);
          this.error.set('');
        },
        error: (err) => {
          this.dashboard.set(null);
          this.handleApiError(err);
        }
      });
  }

  formatMoney(value: number | null): string {
    return value == null ? '—' : new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value);
  }



  updateOrderStatus(order: AdminOrder, status: CustomerOrderStatus): void {
    if (this.updatingOrderIds.has(order.id) || order.status === status) {
      return;
    }

    if (status === 'Cancelled') {
      if (!window.confirm(
        `Are you sure you want to cancel order ${order.orderNumber}?`
      )) {
        return;
      }
    }

    if (status === 'Completed') {
      if (!window.confirm(
        `Mark order ${order.orderNumber} as completed?`
      )) {
        return;
      }
    }

    this.error.set('');
    this.notice = '';
    this.updatingOrderIds.add(order.id);

    this.api.updateOrderStatus(order.id, status)
      .pipe(finalize(() => this.updatingOrderIds.delete(order.id)))
      .subscribe({
        next: () => {
          this.notice = `Order ${order.orderNumber} updated to ${status}.`;

          // Refresh server-backed order and dashboard data.
          this.loadOrders();
          this.loadDashboard();

          // Refresh the timeline if this order's details are open.
          if (this.selectedOrder()?.id === order.id) {
            this.viewOrderDetails(order);
          }
        },
        error: err => {
          this.handleApiError(err);

          // Recover the displayed state from the server.
          this.loadOrders();
          this.loadDashboard();
        }
      });
  }


  get filteredProducts(): AdminProduct[] {
    const search = this.productSearch.trim().toLowerCase();
    return this.products().filter(product => {
      const matchesSearch = !search ||
        product.name.toLowerCase().includes(search) ||
        product.slug.toLowerCase().includes(search) ||
        product.description.toLowerCase().includes(search) ||
        (product.badge ?? '').toLowerCase().includes(search);
      const matchesCategory = this.productCategoryFilter === 'All' ||
        product.categoryId === this.productCategoryFilter;
      const matchesAvailability = this.productAvailabilityFilter === 'All' ||
        (this.productAvailabilityFilter === 'Available' ? product.isAvailable : !product.isAvailable);
      return matchesSearch && matchesCategory && matchesAvailability;
    });
  }

  resolveProductImageUrl(url: string | null): string {
    if (!url) return 'https://images.unsplash.com/photo-1578985545062-69928b1d9587?auto=format&fit=crop&w=180&q=80';
    if (url.startsWith('/')) return `${environment.apiBaseUrl || 'http://localhost:5080'}${url}`;
    return url;
  }

  uploadProductImage(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.error.set('');
    this.productImageUploading.set(true);
    this.api.uploadProductImage(file).pipe(finalize(() => {
      this.productImageUploading.set(false);
      input.value = '';
    })).subscribe({
      next: result => {
        this.productForm.imageUrl = result.mediaUrl;
        this.notice = 'Product image uploaded. Save the product to keep this change.';
      },
      error: err => this.handleApiError(err)
    });
  }

  editProduct(product: AdminProduct): void {
    this.productVariants = [];
    this.resetVariantForm();
    this.loadProductVariants(product.id);
    this.editingProductId = product.id;
    this.productForm = {
      categoryId: product.categoryId,
      name: product.name,
      slug: product.slug,
      description: product.description,
      price: product.price,
      imageUrl: product.imageUrl,
      badge: product.badge,
      isFeatured: product.isFeatured,
      isAvailable: product.isAvailable
    };
    this.activeTab = 'products'; this.notice = ''; this.error.set('');
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }



  saveProduct(): void {
    this.error.set('');
    this.notice = '';

    if (
      !this.productForm.categoryId ||
      !this.productForm.name.trim() ||
      !Number.isFinite(Number(this.productForm.price)) ||
      Number(this.productForm.price) < 0
    ) {
      this.error.set(
        'Add a name, select a category and enter a valid price.'
      );
      return;
    }

    const body: ProductWrite = {
      ...this.productForm,
      name: this.productForm.name.trim(),
      slug: this.productForm.slug.trim(),
      description: this.productForm.description.trim(),
      imageUrl: this.productForm.imageUrl?.trim() || null,
      badge: this.productForm.badge?.trim() || null
    };

    const existingId = this.editingProductId;
    this.saving.set(true);

    const request = existingId
      ? this.api.updateProduct(existingId, body)
      : this.api.createProduct(body);

    request
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: result => {
          const productId = existingId ?? (result as AdminProduct).id;

          this.editingProductId = productId;
          this.notice = existingId
            ? 'Product updated successfully.'
            : 'Product created successfully.';

          this.loadData();
          this.openVariantModal(productId);
        },
        error: err => this.handleApiError(err)
      });
  }



  editCategory(category: AdminCategory): void {
    this.editingCategoryId = category.id;
    this.categoryForm = { name: category.name, slug: category.slug, sortOrder: category.sortOrder, isActive: category.isActive };
    this.activeTab = 'categories'; this.notice = ''; this.error.set('');
  }

  saveCategory(): void {
    this.error.set(''); this.notice = '';
    if (!this.categoryForm.name.trim()) { this.error.set('Category name is required.'); return; }
    const body = { ...this.categoryForm, name: this.categoryForm.name.trim(), slug: this.categoryForm.slug.trim() };
    this.saving.set(true);
    const request = this.editingCategoryId ? this.api.updateCategory(this.editingCategoryId, body) : this.api.createCategory(body);
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.notice = this.editingCategoryId ? 'Category updated successfully.' : 'Category created successfully.'; this.resetCategory(); this.loadData(); },
      error: err => this.handleApiError(err),
    });
  }

  resetProduct(): void {
    this.editingProductId = null;
    this.productForm = this.emptyProduct();
    this.productVariants = [];
    this.resetVariantForm();
  }
  resetCategory(): void { this.editingCategoryId = null; this.categoryForm = this.emptyCategory(); }
  private emptyProduct(): ProductWrite { return { categoryId: '', name: '', slug: '', description: '', price: 0, imageUrl: null, badge: null, isFeatured: false, isAvailable: true }; }
  private emptyCategory(): CategoryWrite { return { name: '', slug: '', sortOrder: 0, isActive: true }; }
  private handleApiError(err: any): void {
    if (err.status === 401 || err.status === 403) {
      this.api.clearToken();
      this.loggedIn.set(false);
      this.error.set('Your session has expired or is not authorized. Please sign in again.'); return;
    }
    this.error.set(err.error?.message ?? (err.status === 0 ? 'Could not reach the API. Start the .NET backend and check its connection string.' : 'Something went wrong. Please review the fields and try again.'));
  }


  getAvailableOrderStatuses(
    order: AdminOrder
  ): CustomerOrderStatus[] {
    const transitions: Record<
      CustomerOrderStatus,
      CustomerOrderStatus[]
    > = {
      Pending: ['Confirmed', 'Cancelled'],
      Confirmed: ['Preparing', 'Cancelled'],
      Preparing: ['Ready', 'Cancelled'],
      Ready: ['Completed', 'Cancelled'],
      Completed: [],
      Cancelled: []
    };

    // Keep the current status visible, but offer only valid next steps.
    return [order.status, ...transitions[order.status]];
  }


  get filteredOrders(): AdminOrder[] {
    const search = this.orderSearch.trim().toLowerCase();

    return this.orders().filter(order => {
      const matchesSearch =
        !search ||
        order.orderNumber.toLowerCase().includes(search) ||
        order.customerName.toLowerCase().includes(search) ||
        order.customerPhone.toLowerCase().includes(search) ||
        order.cakeName.toLowerCase().includes(search);

      const matchesStatus =
        this.orderStatusFilter === 'All' ||
        order.status === this.orderStatusFilter;

      const orderDate = order.preferredDate
        ? order.preferredDate.substring(0, 10)
        : '';

      const matchesDate =
        !this.orderDateFilter ||
        orderDate === this.orderDateFilter;

      return matchesSearch && matchesStatus && matchesDate;
    });
  }

  get sortedOrders(): AdminOrder[] {
    const result = [...this.filteredOrders];

    result.sort((a, b) => {
      if (this.orderSort === 'preferredDate') {
        const dateA = a.preferredDate
          ? new Date(a.preferredDate).getTime()
          : Number.MAX_SAFE_INTEGER;

        const dateB = b.preferredDate
          ? new Date(b.preferredDate).getTime()
          : Number.MAX_SAFE_INTEGER;

        return dateA - dateB;
      }

      const dateA = new Date(a.createdUtc).getTime();
      const dateB = new Date(b.createdUtc).getTime();

      return this.orderSort === 'newest'
        ? dateB - dateA
        : dateA - dateB;
    });

    return result;
  }

  get totalOrderPages(): number {
    return Math.max(1, Math.ceil(this.sortedOrders.length / this.ordersPerPage));
  }

  get paginatedOrders(): AdminOrder[] {
    const start = (this.currentOrderPage - 1) * this.ordersPerPage;

    return this.sortedOrders.slice(start, start + this.ordersPerPage);
  }

  get orderPageNumbers(): number[] {
    return Array.from(
      { length: this.totalOrderPages },
      (_, index) => index + 1
    );
  }

  get firstVisibleOrder(): number {
    return this.sortedOrders.length === 0
      ? 0
      : (this.currentOrderPage - 1) * this.ordersPerPage + 1;
  }

  get lastVisibleOrder(): number {
    return Math.min(
      this.currentOrderPage * this.ordersPerPage,
      this.sortedOrders.length
    );
  }

  goToOrderPage(page: number): void {
    if (page < 1 || page > this.totalOrderPages) {
      return;
    }

    this.currentOrderPage = page;
  }

  onOrderSortChange(): void {
    this.currentOrderPage = 1;
  }

  onOrdersPerPageChange(): void {
    this.currentOrderPage = 1;
  }

  clearOrderFilters(): void {
    this.orderSearch = '';
    this.orderStatusFilter = 'All';
    this.orderDateFilter = '';
    this.orderSort = 'newest';
    this.ordersPerPage = 10;
    this.currentOrderPage = 1;
  }
  viewOrderDetails(order: AdminOrder): void {
    this.selectedOrder.set(order);
    this.orderStatusHistory.set([]);
    this.historyError = '';
    this.historyLoading.set(true);

    this.api.getOrderStatusHistory(order.id)
      .pipe(finalize(() => this.historyLoading.set(false)))
      .subscribe({
        next: history => {
          this.orderStatusHistory.set(history);
        },
        error: err => {
          this.historyError =
            err.error?.message ?? 'Unable to load order status history.';
        }
      });
  }

  closeOrderDetails(): void {
    this.selectedOrder.set(null);
    this.orderStatusHistory.set([]);
    this.historyLoading.set(false);
    this.historyError = '';
    this.resetProduct();
  }

  private emptyExpense(): ExpenseWrite {
    return {
      category: 'Ingredients',
      amount: 0,
      expenseDate: this.localToday(),
      description: null
    };
  }

  private localToday(): string {
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }


  loadExpenses(): void {
    this.loadingExpenses.set(true);
    this.error.set('');

    this.api.getExpenses(
      this.expensePageNumber,
      this.expensePageSize,
      this.expenseFromDate || undefined,
      this.expenseToDate || undefined,
      this.expenseCategoryFilter || undefined
    )
      .pipe(finalize(() => this.loadingExpenses.set(false)))
      .subscribe({
        next: result => {
          this.expensePage.set(result);
          this.expenses.set(result.items);
        },
        error: err => this.handleApiError(err)
      });
  }

  get totalExpenseAmount(): number {
    return this.expenses().reduce((sum, expense) => sum + expense.amount, 0);
  }

  get expenseTotalPages(): number {
    return this.expensePage()?.totalPages ?? 0;
  }

  get expenseFirstVisible(): number {
    const page = this.expensePage();
    return page?.totalCount
      ? (page.page - 1) * page.pageSize + 1
      : 0;
  }

  get expenseLastVisible(): number {
    const page = this.expensePage();
    return page
      ? Math.min(
        page.page * page.pageSize,
        page.totalCount
      )
      : 0;
  }

  applyExpenseFilters(): void {
    this.expensePageNumber = 1;
    this.loadExpenses();
  }

  clearExpenseFilters(): void {
    this.expenseFromDate = '';
    this.expenseToDate = '';
    this.expenseCategoryFilter = '';
    this.expensePageNumber = 1;
    this.loadExpenses();
  }

  changeExpensePage(page: number): void {
    if (page < 1 || page > this.expenseTotalPages) return;
    this.expensePageNumber = page;
    this.loadExpenses();
  }

  changeExpensePageSize(size: number): void {
    this.expensePageSize = size;
    this.expensePageNumber = 1;
    this.loadExpenses();
  }

  editExpense(expense: AdminExpense): void {
    this.editingExpenseId = expense.id;
    this.expenseForm = {
      category: expense.category,
      amount: expense.amount,
      expenseDate: expense.expenseDate.substring(0, 10),
      description: expense.description
    };
    this.error.set('');
    this.notice = '';
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  resetExpense(): void {
    this.editingExpenseId = null;
    this.expenseForm = this.emptyExpense();
  }

  saveExpense(): void {
    this.error.set('');
    this.notice = '';

    const category = this.expenseForm.category.trim();
    const amount = Number(this.expenseForm.amount);
    const expenseDate = this.expenseForm.expenseDate;
    const description = this.expenseForm.description?.trim() || null;

    if (!category || category.length > 80) {
      this.error.set('Enter a category of up to 80 characters.');
      return;
    }

    if (!Number.isFinite(amount) || amount <= 0) {
      this.error.set('Expense amount must be greater than zero.');
      return;
    }

    if (Math.round(amount * 100) !== amount * 100) {
      this.error.set('Expense amount cannot have more than two decimal places.');
      return;
    }

    if (!expenseDate || !/^\d{4}-\d{2}-\d{2}$/.test(expenseDate)) {
      this.error.set('Select a valid expense date.');
      return;
    }

    if (description && description.length > 500) {
      this.error.set('Description cannot exceed 500 characters.');
      return;
    }

    const body: ExpenseWrite = {
      category,
      amount,
      expenseDate,
      description
    };

    const editingId = this.editingExpenseId;
    this.expenseSaving.set(true);

    const request = editingId
      ? this.api.updateExpense(editingId, body)
      : this.api.createExpense(body);

    request
      .pipe(finalize(() => this.expenseSaving.set(false)))
      .subscribe({
        next: () => {
          this.notice = editingId
            ? 'Expense updated successfully.'
            : 'Expense added successfully.';

          this.resetExpense();
          this.loadExpenses();
        },
        error: err => this.handleApiError(err)
      });
  }

  deleteExpense(expense: AdminExpense): void {
    const confirmed = window.confirm(
      `Delete the ${this.formatMoney(expense.amount)} expense for "${expense.category}"?`
    );

    if (!confirmed) return;

    this.error.set('');
    this.notice = '';
    this.deletingExpenseId = expense.id;

    this.api.deleteExpense(expense.id)
      .pipe(finalize(() => this.deletingExpenseId = null))
      .subscribe({
        next: () => {
          this.notice = 'Expense deleted successfully.';

          if (this.expenses().length === 1 && this.expensePageNumber > 1) {
            this.expensePageNumber--;
          }

          this.loadExpenses();
        },
        error: err => this.handleApiError(err)
      });
  }

  private firstDayOfMonth(): string {
    const today = this.localToday();
    return `${today.substring(0, 7)}-01`;
  }

  loadProfitReport(): void {
    this.error.set('');
    this.notice = '';

    if (!this.profitFromDate || !this.profitToDate) {
      this.error.set('Select both report dates.');
      return;
    }

    if (this.profitFromDate > this.profitToDate) {
      this.error.set('From date must be on or before the to date.');
      return;
    }

    const start = new Date(`${this.profitFromDate}T00:00:00Z`);
    const end = new Date(`${this.profitToDate}T00:00:00Z`);
    const days = Math.round((end.getTime() - start.getTime()) / 86400000);

    if (days > 366) {
      this.error.set('Select a date range of no more than 367 calendar days.');
      return;
    }

    this.profitReportLoading.set(true);

    this.api.getProfitReport(this.profitFromDate, this.profitToDate)
      .pipe(finalize(() => this.profitReportLoading.set(false)))
      .subscribe({
        next: result => this.profitReport.set(result),
        error: err => {
          this.profitReport.set(null);
          this.handleApiError(err);
        }
      });
  }


  loadProductVariants(productId: string): void {
    this.variantsLoading.set(true);
    this.productVariants = [];
    this.error.set('');

    this.api.getProductVariants(productId)
      .pipe(finalize(() => this.variantsLoading.set(false)))
      .subscribe({
        next: variants => {
          this.productVariants = variants;
        },
        error: err => {
          this.productVariants = [];
          this.handleApiError(err);
        }
      });
  }


  startVariantEdit(variant: AdminProductVariant): void {
    this.editingVariantId = variant.id;
    this.variantForm = {
      sizeLabel: variant.sizeLabel,
      weightInGrams: variant.weightInGrams,
      price: variant.price,
      isAvailable: variant.isAvailable
    };
  }

  resetVariantForm(): void {
    this.editingVariantId = null;
    this.variantForm = this.emptyVariant();
  }

  saveVariant(): void {
    const productId = this.editingProductId;

    if (!productId) {
      this.error.set('Save the product before adding sizes and prices.');
      return;
    }

    const label = this.variantForm.sizeLabel.trim();
    const weight = Number(this.variantForm.weightInGrams);
    const price = Number(this.variantForm.price);

    if (!label || label.length > 30 || !Number.isInteger(weight) ||
      weight <= 0 || !Number.isFinite(price) || price <= 0 ||
      Math.round(price * 100) !== price * 100) {
      this.error.set('Enter a size label, a valid weight and a price with at most two decimal places.');
      return;
    }

    const body: ProductVariantWrite = {
      ...this.variantForm,
      sizeLabel: label,
      weightInGrams: weight,
      price
    };

    this.error.set('');
    this.notice = '';
    this.variantSaving.set(true);

    const request = this.editingVariantId
      ? this.api.updateProductVariant(
        productId, this.editingVariantId, body)
      : this.api.createProductVariant(productId, body);

    request.pipe(finalize(() => this.variantSaving.set(false)))
      .subscribe({
        next: () => {
          this.notice = this.editingVariantId
            ? 'Size and price updated.'
            : 'Size and price added.';
          this.resetVariantForm();
          this.loadProductVariants(productId);
        },
        error: err => this.handleApiError(err)
      });
  }


  openVariantModal(productId: string): void {
    this.editingProductId = productId;
    this.showVariantModal.set(true);
    this.productVariants = [];
    this.resetVariantForm();
    this.error.set('');
    this.loadProductVariants(productId);
  }

  closeVariantModal(): void {
    this.showVariantModal.set(false);
    this.resetProduct();
    this.loadData();
  }

  manageProductSizes(product: AdminProduct): void {
    this.editProduct(product);
    this.openVariantModal(product.id);
  }

}
