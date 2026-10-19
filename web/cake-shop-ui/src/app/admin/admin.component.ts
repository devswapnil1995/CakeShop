import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  AdminApiService, AdminCategory, AdminProduct, CategoryWrite, ProductWrite, AdminOrder,
  CustomerOrderStatus,
  AdminDashboardSummary, OrderStatusHistory
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
  loggedIn = false;
  loading = false;
  saving = false;
  activeTab: 'dashboard' | 'products' | 'categories' | 'orders' = 'dashboard';
  dashboard: AdminDashboardSummary | null = null;
  dashboardLoading = false;
  error = '';
  notice = '';
  email = '';
  password = '';
  products: AdminProduct[] = [];
  categories: AdminCategory[] = [];
  editingProductId: string | null = null;
  editingCategoryId: string | null = null;

  productForm: ProductWrite = this.emptyProduct();
  categoryForm: CategoryWrite = this.emptyCategory();

  orders: AdminOrder[] = [];
  loadingOrders = false;
  updatingOrderIds = new Set<string>();

  orderSearch = '';
  orderStatusFilter: CustomerOrderStatus | 'All' = 'All';
  orderDateFilter = '';

  selectedOrder: AdminOrder | null = null;

  readonly orderStatuses: CustomerOrderStatus[] = [
    'Pending',
    'Confirmed',
    'Preparing',
    'Ready',
    'Completed',
    'Cancelled'
  ];

  orderStatusHistory: OrderStatusHistory[] = [];
  historyLoading = false;
  historyError = '';


  orderSort: 'newest' | 'oldest' | 'preferredDate' = 'newest';

  ordersPerPage = 10;
  currentOrderPage = 1;

  readonly orderPageSizes = [5, 10, 20, 50];

  get featuredCount(): number { return this.products.filter(product => product.isFeatured).length; }

  ngOnInit(): void {
    this.loggedIn = this.api.isLoggedIn;
    if (this.loggedIn) { this.loadData(); this.loadDashboard(); }
  }

  login(): void {
    this.error = ''; this.notice = ''; this.loading = true;
    this.api.login(this.email.trim(), this.password).pipe(finalize(() => this.loading = false)).subscribe({
      next: result => { this.api.saveToken(result.accessToken); this.loggedIn = true; this.password = ''; this.loadData(); this.loadDashboard(); },
      error: err => { this.error = err.status === 401 ? 'Email or password is incorrect.' : (err.error?.message ?? 'Unable to sign in. Check that the API is running and the admin account has been seeded.'); },
    });
  }

  logout(): void {
    this.api.clearToken();
    this.loggedIn = false;
    this.products = [];
    this.categories = [];
    this.orders = [];
    this.selectedOrder = null;
    this.resetProduct();
    this.error = '';
    this.orderStatusHistory = [];
    this.historyLoading = false;
    this.historyError = '';
  }

  loadData(): void {
    this.loading = true; this.error = '';
    let completed = 0;
    const done = () => { completed++; if (completed === 2) this.loading = false; };
    this.api.getCategories().pipe(finalize(done)).subscribe({ next: rows => this.categories = rows, error: err => this.handleApiError(err) });
    this.api.getProducts().pipe(finalize(done)).subscribe({ next: rows => this.products = rows, error: err => this.handleApiError(err) });
  }

  selectTab(
    tab: 'dashboard' | 'products' | 'categories' | 'orders'
  ): void {
    this.activeTab = tab;
    this.error = '';
    this.notice = '';

    if (tab === 'orders') {
      this.loadOrders();
    }

    if (tab === 'dashboard') {
      this.loadDashboard();
    }
  }

  loadOrders(): void {
    this.loadingOrders = true;
    this.error = '';

    this.api.getOrders()
      .pipe(finalize(() => this.loadingOrders = false))
      .subscribe({
        next: orders => {
          this.orders = orders;

          if (this.currentOrderPage > this.totalOrderPages) {
            this.currentOrderPage = this.totalOrderPages;
          }
        },
        error: err => this.handleApiError(err)
      });
  }


  loadDashboard(): void {
    if (!this.loggedIn) return;

    this.dashboardLoading = true;

    this.api.getDashboardSummary()
      .pipe(finalize(() => {
        this.dashboardLoading = false;
      }))
      .subscribe({
        next: (result) => {
          this.dashboard = result;
          this.error = '';
        },
        error: (err) => {
          this.dashboard = null;
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

    this.error = '';
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
          if (this.selectedOrder?.id === order.id) {
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


  editProduct(product: AdminProduct): void {
    this.editingProductId = product.id;
    this.productForm = { categoryId: product.categoryId, name: product.name, slug: product.slug, description: product.description, price: product.price, imageUrl: product.imageUrl, badge: product.badge, isFeatured: product.isFeatured, isAvailable: product.isAvailable };
    this.activeTab = 'products'; this.notice = ''; this.error = '';
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  saveProduct(): void {
    this.error = ''; this.notice = '';
    if (!this.productForm.categoryId || !this.productForm.name.trim() || this.productForm.price < 0) { this.error = 'Add a name, select a category and enter a valid price.'; return; }
    const body = { ...this.productForm, name: this.productForm.name.trim(), slug: this.productForm.slug.trim(), description: this.productForm.description.trim(), imageUrl: this.productForm.imageUrl?.trim() || null, badge: this.productForm.badge?.trim() || null };
    this.saving = true;
    const request = this.editingProductId ? this.api.updateProduct(this.editingProductId, body) : this.api.createProduct(body);
    request.pipe(finalize(() => this.saving = false)).subscribe({
      next: () => { this.notice = this.editingProductId ? 'Product updated successfully.' : 'Product created successfully.'; this.resetProduct(); this.loadData(); },
      error: err => this.handleApiError(err),
    });
  }

  editCategory(category: AdminCategory): void {
    this.editingCategoryId = category.id;
    this.categoryForm = { name: category.name, slug: category.slug, sortOrder: category.sortOrder, isActive: category.isActive };
    this.activeTab = 'categories'; this.notice = ''; this.error = '';
  }

  saveCategory(): void {
    this.error = ''; this.notice = '';
    if (!this.categoryForm.name.trim()) { this.error = 'Category name is required.'; return; }
    const body = { ...this.categoryForm, name: this.categoryForm.name.trim(), slug: this.categoryForm.slug.trim() };
    this.saving = true;
    const request = this.editingCategoryId ? this.api.updateCategory(this.editingCategoryId, body) : this.api.createCategory(body);
    request.pipe(finalize(() => this.saving = false)).subscribe({
      next: () => { this.notice = this.editingCategoryId ? 'Category updated successfully.' : 'Category created successfully.'; this.resetCategory(); this.loadData(); },
      error: err => this.handleApiError(err),
    });
  }

  resetProduct(): void { this.editingProductId = null; this.productForm = this.emptyProduct(); }
  resetCategory(): void { this.editingCategoryId = null; this.categoryForm = this.emptyCategory(); }
  private emptyProduct(): ProductWrite { return { categoryId: '', name: '', slug: '', description: '', price: 0, imageUrl: null, badge: null, isFeatured: false, isAvailable: true }; }
  private emptyCategory(): CategoryWrite { return { name: '', slug: '', sortOrder: 0, isActive: true }; }
  private handleApiError(err: any): void {
    if (err.status === 401 || err.status === 403) { this.api.clearToken(); this.loggedIn = false; this.error = 'Your session has expired or is not authorized. Please sign in again.'; return; }
    this.error = err.error?.message ?? (err.status === 0 ? 'Could not reach the API. Start the .NET backend and check its connection string.' : 'Something went wrong. Please review the fields and try again.');
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

    return this.orders.filter(order => {
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
    this.selectedOrder = order;
    this.orderStatusHistory = [];
    this.historyError = '';
    this.historyLoading = true;

    this.api.getOrderStatusHistory(order.id)
      .pipe(finalize(() => this.historyLoading = false))
      .subscribe({
        next: history => {
          this.orderStatusHistory = history;
        },
        error: err => {
          this.historyError =
            err.error?.message ?? 'Unable to load order status history.';
        }
      });
  }

  closeOrderDetails(): void {
    this.selectedOrder = null;
    this.orderStatusHistory = [];
    this.historyLoading = false;
    this.historyError = '';
  }

}
