export function mapLoan(dto) {
  if (!dto) return dto;
  return {
    ...dto,
    // Fix deep props (Loans.jsx reads .customer.fullName and .product.productName)
    customer: dto.customer || {
      fullName: dto.customerName,
      phone: dto.customerPhone // note: might not present in loanDto but frontend expects it
    },
    product: dto.product || {
      productName: dto.productName
    }
  };
}

export function mapLoanList(res) {
  if (!res) return res;
  if (Array.isArray(res)) return res.map(mapLoan);
  if (res.items && Array.isArray(res.items)) {
    return {
      ...res,
      items: res.items.map(mapLoan)
    };
  }
  return res;
}
