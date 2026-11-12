import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { timer, switchMap, takeWhile, catchError, of } from 'rxjs';
import { CreateOrderRequest, OrderResponse } from './order.models';
import { OrderService } from './order.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  private readonly orders = inject(OrderService);
  request: CreateOrderRequest = { customerId: '', sku: '', quantity: 1, unitPrice: 0 };
  order: OrderResponse | null = null;
  customerOrders: OrderResponse[] = [];
  loading = false;
  error = '';

  submit(): void {
    this.error = '';
    this.order = null;
    this.loading = true;
    this.orders.createOrder(this.request).subscribe({
      next: order => {
        this.order = order;
        this.watchStatus(order.id);
      },
      error: error => { this.error = error?.error?.title ?? 'Could not create the order.'; this.loading = false; }
    });
  }

  loadCustomerOrders(): void {
    if (!this.request.customerId.trim()) return;
    this.orders.getCustomerOrders(this.request.customerId.trim()).subscribe({
      next: orders => this.customerOrders = orders,
      error: () => this.error = 'Could not load customer orders.'
    });
  }

  private watchStatus(id: string): void {
    timer(1000, 1500).pipe(
      switchMap(() => this.orders.getOrder(id)),
      takeWhile(current => current.status === 'Pending', true),
      catchError(() => of(this.order!))
    ).subscribe({ next: current => { this.order = current; if (current.status !== 'Pending') this.loading = false; } });
  }
}
