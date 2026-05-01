import { httpClient } from '../../../shared/api/httpClient';

function normalizeConcentrationReport(report) {
  if (!report) return report;
  if (Array.isArray(report.limits)) return report;

  const borrowerLimits = (report.topBorrowers || []).map(item => ({
    id: item.customerId,
    customerName: item.customerName,
    limitType: 'Single borrower',
    currentExposure: item.totalExposure,
    limitAmount: report.bankEquity ? report.bankEquity * ((item.limitPercentage || 0) / 100) : 0,
    utilizationRatio: item.equityShare,
    limitPercentage: item.limitPercentage,
    isBreached: item.isBreached,
  }));

  const industryLimits = (report.industryConcentrations || []).map(item => ({
    id: item.industryCode,
    groupName: item.industryCode,
    limitType: 'Industry',
    currentExposure: item.totalExposure,
    limitAmount: report.totalPortfolioExposure ? report.totalPortfolioExposure * ((item.limitPercentage || 0) / 100) : 0,
    utilizationRatio: item.portfolioShare,
    limitPercentage: item.limitPercentage,
    isBreached: item.isBreached,
  }));

  return {
    ...report,
    limits: [...borrowerLimits, ...industryLimits],
  };
}

export const riskApi = {
  getCapitalAdequacy: () => httpClient('GET', '/capital-adequacy/latest'),
  getConcentrationRisk: async () => normalizeConcentrationReport(await httpClient('GET', '/risk/concentration')),
  getExpectedLossSummary: () => httpClient('GET', '/risk/el-summary'),
  getStressTestResults: () => httpClient('POST', '/risk/stress-test'),
  getLiquidityReport: () => httpClient('GET', '/reports/regulatory/liquidity'),
  runStressTest: () => httpClient('POST', '/risk/stress-test'),
  getBalanceSheet: (asOfDate) => httpClient('GET', `/accounting/balance-sheet?asOfDate=${asOfDate}`),
  getPnl: (fromDate, toDate) => httpClient('GET', `/accounting/pnl?fromDate=${fromDate}&toDate=${toDate}`),
};
