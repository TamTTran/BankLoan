export function mapApplication(dto) {
  if (!dto) return dto;
  return {
    ...dto,
    // Fix missing deep props (Applications.jsx reads .customer.fullName and .product.productName)
    customer: dto.customer || {
      fullName: dto.customerName,
      phone: dto.customerPhone
    },
    product: dto.product || {
      productName: dto.productName
    }
  };
}

export function mapApplicationList(res) {
  if (!res) return res;
  if (Array.isArray(res)) return res.map(mapApplication);
  if (res.items && Array.isArray(res.items)) {
    return {
      ...res,
      items: res.items.map(mapApplication)
    };
  }
  return res;
}
