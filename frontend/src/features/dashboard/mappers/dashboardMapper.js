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
    totalApplicationsToday: dto.totalApplicationsToday ?? 0,
    approvedApplicationsThisMonth: dto.approvedApplicationsThisMonth ?? 0,
    rejectedApplicationsThisMonth: dto.rejectedApplicationsThisMonth ?? 0,
    disbursedLoansThisMonth: dto.disbursedLoansThisMonth ?? 0,
    applicationStatusBreakdown: dto.applicationStatusBreakdown ?? [],
  };
}
