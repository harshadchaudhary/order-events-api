export interface CreateOrderRequest {
  customerId: string;
  sku: string;
  quantity: number;
  unitPrice: number;
}

export interface OrderResponse {
  id: string;
  customerId: string;
  sku: string;
  quantity: number;
  totalAmount: number;
  status: string;
  statusReason: string | null;
  placedAt: string;
  processedAt: string | null;
}
