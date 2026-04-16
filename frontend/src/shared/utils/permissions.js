export function buildPermissions(role) {
  const isRole = (...roles) => roles.includes(role);
  return {
    createCustomer:     isRole('Admin', 'LoanOfficer'),
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

    // For navigation layout visibility
    viewProductsNav:    isRole('Admin', 'LoanOfficer', 'Approver', 'Manager'),
  };
}

