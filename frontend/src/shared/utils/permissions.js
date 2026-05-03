export function buildPermissions(role) {
  const isRole = (...roles) => roles.includes(role);
  return {
    createCustomer:     isRole('Admin', 'LoanOfficer'),
    createProduct:      isRole('Admin', 'LoanOfficer'),
    updateProduct:      isRole('Admin'),
    createApplication:  isRole('Admin', 'LoanOfficer'),
    submitApplication:  isRole('Admin', 'LoanOfficer'),
    approveApplication: isRole('Admin', 'Approver'),
    createLoan:         isRole('Admin', 'LoanOfficer'),
    disburse:           isRole('Admin', 'LoanOfficer'),
    recordPayment:      isRole('Admin', 'LoanOfficer'),
    addCollectionNote:  isRole('Admin', 'Collector'),
    manageProducts:     isRole('Admin'),
    viewOverdue:        isRole('Admin', 'Collector', 'Manager', 'LoanOfficer'),
    
    // Wave 3 — Collections
    viewCollectionQueue:   isRole('Admin', 'Collector', 'Manager'),
    viewAllCollections:    isRole('Admin', 'Manager'),
    assignCollector:       isRole('Admin', 'Manager'),
    logCollectionAction:   isRole('Admin', 'Collector'),
    recordPromise:         isRole('Admin', 'Collector'),

    // Wave 3 — Reports
    viewReports:        isRole('Admin', 'Manager'),
    viewApprovalReport: isRole('Admin', 'Manager', 'Approver'),
    viewNapasNav:       isRole('Admin', 'Manager', 'LoanOfficer', 'Collector'),
    viewAmlNav:         isRole('Admin', 'Manager', 'ComplianceOfficer'),
    viewRiskNav:        isRole('Admin', 'Manager', 'RiskOfficer'),
    viewRegulatoryNav:  isRole('Admin', 'Manager'),
    viewInterbankNav:   isRole('Admin', 'Manager'),
    viewAccountingReports: isRole('Admin', 'FinanceManager'),

    // Wave 4 & 5 — Ops, Approvals & Advanced Lending
    viewApprovalsNav:      isRole('Admin', 'Approver', 'Manager'),
    decideApproval:        isRole('Admin', 'Approver', 'Manager'),
    viewSystemOpsNav:      isRole('Admin'),
    runBatchJobs:          isRole('Admin'),
    requestRestructure:    isRole('Admin', 'Manager', 'LoanOfficer'),
    requestWriteOff:       isRole('Admin', 'Manager'),
    requestManualAdjust:   isRole('Admin', 'Manager', 'LoanOfficer'),

    // For navigation layout visibility
    viewProductsNav:    isRole('Admin', 'LoanOfficer', 'Approver', 'Manager'),
  };
}
