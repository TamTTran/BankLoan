// DelinquencyStatus entity removed in v2.
// Overdue tracking is now done query-time per §12.8 of the blueprint.
// DPD and bucket are computed in LoanService.GetOverdueListAsync().
namespace LoanFlow.Domain.Entities;
