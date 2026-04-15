import { describe, it, expect } from 'vitest';
import { mapApplication } from '../src/features/applications/mappers/applicationMapper';

describe('applicationMapper', () => {
  it('should handle null input', () => {
    expect(mapApplication(null)).toBeNull();
  });

  it('should fix nested objects based on flat DTO properties', () => {
    const input = {
      id: 1,
      customerName: 'Nguyen Van A',
      customerPhone: '0901234567',
      productName: 'Consumer Loan'
    };

    const output = mapApplication(input);
    
    expect(output.customer).toBeDefined();
    expect(output.customer.fullName).toBe('Nguyen Van A');
    expect(output.customer.phone).toBe('0901234567');
    
    expect(output.product).toBeDefined();
    expect(output.product.productName).toBe('Consumer Loan');
    
    // original properties should remain
    expect(output.id).toBe(1);
    expect(output.customerName).toBe('Nguyen Van A');
  });

  it('should ignore if nested objects already exist', () => {
    const input = {
      id: 2,
      customer: { fullName: 'B', phone: '090' },
      product: { productName: 'Car' }
    };

    const output = mapApplication(input);

    expect(output.customer.fullName).toBe('B');
    expect(output.product.productName).toBe('Car');
  });
});
