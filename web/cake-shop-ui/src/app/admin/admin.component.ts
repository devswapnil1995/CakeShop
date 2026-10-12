import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { AdminApiService, AdminCategory, AdminProduct, CategoryWrite, ProductWrite } from './admin-api.service';

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
  activeTab: 'products' | 'categories' = 'products';
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

  get featuredCount(): number { return this.products.filter(product => product.isFeatured).length; }

  ngOnInit(): void {
    this.loggedIn = this.api.isLoggedIn;
    if (this.loggedIn) this.loadData();
  }

  login(): void {
    this.error = ''; this.notice = ''; this.loading = true;
    this.api.login(this.email.trim(), this.password).pipe(finalize(() => this.loading = false)).subscribe({
      next: result => { this.api.saveToken(result.accessToken); this.loggedIn = true; this.password = ''; this.loadData(); },
      error: err => { this.error = err.status === 401 ? 'Email or password is incorrect.' : (err.error?.message ?? 'Unable to sign in. Check that the API is running and the admin account has been seeded.'); },
    });
  }

  logout(): void { this.api.clearToken(); this.loggedIn = false; this.products = []; this.categories = []; this.resetProduct(); this.error = ''; }

  loadData(): void {
    this.loading = true; this.error = '';
    let completed = 0;
    const done = () => { completed++; if (completed === 2) this.loading = false; };
    this.api.getCategories().pipe(finalize(done)).subscribe({ next: rows => this.categories = rows, error: err => this.handleApiError(err) });
    this.api.getProducts().pipe(finalize(done)).subscribe({ next: rows => this.products = rows, error: err => this.handleApiError(err) });
  }

  selectTab(tab: 'products' | 'categories'): void { this.activeTab = tab; this.error = ''; this.notice = ''; }

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
}
