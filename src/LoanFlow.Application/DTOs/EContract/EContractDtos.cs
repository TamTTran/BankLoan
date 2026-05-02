using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using System.Text;

namespace LoanFlow.Application.Services;

public class EContractDto
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public string ContractNumber { get; set; } = default!;
    public string PdfContentHash { get; set; } = default!;
    public string DigitalSignature { get; set; } = default!;
    public string ClientIpAddress { get; set; } = default!;
    public string? PdfStoragePath { get; set; }
    public DateTime SignedAt { get; set; }
}
