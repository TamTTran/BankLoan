using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

public class ExposureLimitDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal MaxExposure { get; set; }
    public decimal CurrentExposure { get; set; }
    public decimal UtilizationRate { get; set; }
    public string Status { get; set; } = "";
}

public class SetExposureLimitRequest
{
    public decimal MaxExposure { get; set; }
}
