using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace LoanFlow.Application.Services;

public class EContractDto
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public string ContractNumber { get; set; } = default!;
    public string PdfContentHash { get; set; } = default!;
    public string MockDigitalSignature { get; set; } = default!;
    public string ClientIpAddress { get; set; } = default!;
    public DateTime SignedAt { get; set; }
}

public interface IEContractService
{
    Task<Result<EContractDto>> SignContractAsync(int loanId, string ipAddress);
    Task<Result<EContractDto>> GetByLoanIdAsync(int loanId);
}

public class EContractService : IEContractService
{
    private readonly LoanFlowDbContext _db;

    public EContractService(LoanFlowDbContext db)
    {
        _db = db;
    }

    public async Task<Result<EContractDto>> SignContractAsync(int loanId, string ipAddress)
    {
        var loan = await _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.Application)
            .FirstOrDefaultAsync(l => l.Id == loanId);

        if (loan == null) return Result.Failure<EContractDto>("Loan not found.");
        
        var existing = await _db.EContracts.FirstOrDefaultAsync(e => e.LoanId == loanId);
        if (existing != null) return Result.Failure<EContractDto>("Contract is already signed.");

        // 1. Generate Virtual Contract Document Text
        var contractContent = $"LOAN CONTRACT\n" +
                              $"No: {loan.LoanAccountNo}\n" +
                              $"Borrower: {loan.Customer.FullName}\n" +
                              $"ID Number: {loan.Customer.IdNumber}\n" +
                              $"Principal: {loan.ApprovedAmount:N0} VND\n" +
                              $"Tenor: {loan.TenorMonths} months\n" +
                              $"Signed at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n";

        // 2. Hash Document (SHA-256)
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(contractContent));
        var hashString = Convert.ToHexString(hashBytes);

        // 3. Mock RSA Private Key Signature for the Hash
        // In a real system, this is signed by a HSM (Hardware Security Module) API.
        var mockRsaSignature = Convert.ToBase64String(Encoding.UTF8.GetBytes($"SIGNED_RSA_{hashString}_KEY{Guid.NewGuid()}"));

        var contract = new EContract
        {
            LoanId = loanId,
            ContractNumber = "CT-" + loan.LoanAccountNo,
            PdfContentHash = hashString,
            MockDigitalSignature = mockRsaSignature,
            ClientIpAddress = ipAddress,
            SignedAt = DateTime.UtcNow
        };

        _db.EContracts.Add(contract);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = 0, // System context or user context depending on how it's called
            ActionCode = "CONTRACT_SIGNED",
            EntityType = "EContract",
            EntityId = 0, // will be generated
            NewValuesJson = AuditHelper.ToJson(new { loanId = loanId, hash = hashString })
        });

        await _db.SaveChangesAsync(); // save to get ID

        return Result.Success(new EContractDto
        {
            Id = contract.Id,
            LoanId = contract.LoanId,
            ContractNumber = contract.ContractNumber,
            PdfContentHash = contract.PdfContentHash,
            MockDigitalSignature = contract.MockDigitalSignature,
            ClientIpAddress = contract.ClientIpAddress,
            SignedAt = contract.SignedAt
        });
    }

    public async Task<Result<EContractDto>> GetByLoanIdAsync(int loanId)
    {
        var e = await _db.EContracts.FirstOrDefaultAsync(x => x.LoanId == loanId);
        if (e == null) return Result.Failure<EContractDto>("Not found");

        return Result.Success(new EContractDto
        {
            Id = e.Id,
            LoanId = e.LoanId,
            ContractNumber = e.ContractNumber,
            PdfContentHash = e.PdfContentHash,
            MockDigitalSignature = e.MockDigitalSignature,
            ClientIpAddress = e.ClientIpAddress,
            SignedAt = e.SignedAt
        });
    }
}
