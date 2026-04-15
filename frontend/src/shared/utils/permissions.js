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
    
    // For navigation layout visibility
    viewProductsNav:    isRole('Admin', 'LoanOfficer', 'Approver', 'Manager'),
  };
}
