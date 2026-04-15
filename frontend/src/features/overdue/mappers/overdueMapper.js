export function mapOverdueItem(dto) {
  if (!dto) return dto;
  return {
    ...dto,
    // Fix frontend looking for 'overdueAmount' but DTO giving 'overdueDueAmount'
    overdueAmount: dto.overdueDueAmount,
    // Fix frontend looking for 'daysOverdue' but DTO giving 'dpd'
    daysOverdue: dto.dpd,
    // Add default fallbacks for nested objects
    customer: dto.customer || {
      fullName: dto.customerName,
      phone: dto.customerPhone
    }
  };
}

export function mapOverdueList(res) {
  if (!res) return res;
  if (Array.isArray(res)) return res.map(mapOverdueItem);
  if (res.items && Array.isArray(res.items)) {
    return {
      ...res,
      items: res.items.map(mapOverdueItem)
    };
  }
  return res;
}
