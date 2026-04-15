export function mapDashboardSummary(dto) {
  if (!dto) return dto;
  return {
    ...dto,
    // Map existing backend flat props to frontend identifiers
    totalOutstanding: dto.totalOutstandingAmount,
    overdueAccounts: dto.totalOverdueLoans,
    // Provide defaults for missing ones
    totalDisbursedThisMonth: dto.totalDisbursedThisMonth ?? 0,
    collectedThisMonth: dto.collectedThisMonth ?? 0,
  };
}
