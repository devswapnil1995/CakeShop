import { Component, OnInit, inject } from '@angular/core';
import { catchError, of } from 'rxjs';
import { CatalogApiService } from './catalog-api.service';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';

interface CakeProduct {
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
  imports: [CommonModule, RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})
export class AppComponent implements OnInit {
  readonly isAdminRoute = typeof window !== 'undefined' && window.location.pathname.startsWith('/admin');
  private readonly catalogApi = inject(CatalogApiService);
  apiConnected = false;
  readonly brandName = 'The Sweet Panda';
  // Replace with the shop's WhatsApp number in international format, digits only.
  private readonly WHATSAPP_NUMBER = '919000000000';

  activeCategory = 'All';
  mobileMenuOpen = false;

  categories = ['All', 'Celebration', 'Chocolate', 'Signature'];

  products: CakeProduct[] = [
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
  ];

  ngOnInit(): void {
    this.catalogApi.getProducts().pipe(catchError(() => of(null))).subscribe((items) => {
      if (!items) return;
      this.apiConnected = true;
      this.products = items.map((item) => ({
        name: item.name, description: item.description, price: `From ₹${item.price}`,
        tag: item.badge ?? undefined, image: item.imageUrl ?? '',
        category: this.toDisplayCategory(item.categoryName),
      }));
      const categoryNames = [...new Set(items.map((item) => this.toDisplayCategory(item.categoryName)))];
      this.categories.splice(0, this.categories.length, 'All', ...categoryNames);
    });
  }

  private toDisplayCategory(name: string): string {
    return name;
  }

  get visibleProducts(): CakeProduct[] {
    return this.activeCategory === 'All'
      ? this.products
      : this.products.filter((product) => product.category === this.activeCategory);
  }

  setCategory(category: string): void {
    this.activeCategory = category;
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen = !this.mobileMenuOpen;
  }

  orderOnWhatsApp(productName?: string): void {
    const message = productName
      ? `Hi ${this.brandName}! I'd like to know more about the ${productName}.`
      : `Hi ${this.brandName}! I'd like to explore your cakes and place an order.`;
    const url = `https://wa.me/${this.WHATSAPP_NUMBER}?text=${encodeURIComponent(message)}`;
    window.open(url, '_blank', 'noopener,noreferrer');
  }
}
