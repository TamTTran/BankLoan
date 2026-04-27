import { httpClient } from '../../../shared/api/httpClient';

export const riskApi = {
  getCapitalAdequacy: () => httpClient('GET', '/capital-adequacy/latest'),
  getConcentrationRisk: () => httpClient('GET', '/concentration-limits/summary'),
  getStressTestResults: () => httpClient('GET', '/risk/stress-tests'),
  getLiquidityReport: () => httpClient('GET', '/risk/liquidity'),
  runStressTest: (scenarioId) => httpClient('POST', `/risk/stress-tests/${scenarioId}/run`),
  getBalanceSheet: (asOfDate) => httpClient('GET', `/accounting/balance-sheet?asOfDate=${asOfDate}`),
  getPnl: (fromDate, toDate) => httpClient('GET', `/accounting/pnl?fromDate=${fromDate}&toDate=${toDate}`),
};
