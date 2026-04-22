using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

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

public interface IEContractService
{
    Task<Result<EContractDto>> SignContractAsync(int loanId, string ipAddress);
    Task<Result<EContractDto>> GetByLoanIdAsync(int loanId);
}

public interface ICertificateAuthorityProvider
{
    Task<string> SignHashAsync(string hashStr);
}

// Mock implementation of the CA provider
public class MockCertificateAuthorityProvider : ICertificateAuthorityProvider
{
    public Task<string> SignHashAsync(string hashStr)
    {
        // Simulated VNPT-CA / FPT-CA remote signing
        var simulatedSignature = Convert.ToBase64String(Encoding.UTF8.GetBytes($"RSA_SIGNED_{hashStr}_BY_VNPT_CA_{Guid.NewGuid()}"));
        return Task.FromResult(simulatedSignature);
    }
}

public class EContractService : IEContractService
{
    private readonly LoanFlowDbContext _db;
    private readonly ICertificateAuthorityProvider _caProvider;
    private const string ContractStorageRoot = "App_Data";
    private const string ContractStorageFolder = "EContracts";

    public EContractService(LoanFlowDbContext db, ICertificateAuthorityProvider caProvider)
    {
        _db = db;
        _caProvider = caProvider;
        QuestPDF.Settings.License = LicenseType.Community;
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

        // 1. Generate Virtual Contract Document Text using QuestPDF
        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .Text("LOAN CONTRACT (DIGITAL)")
                    .SemiBold().FontSize(20).FontColor(Colors.Blue.Darken2);

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(x =>
                    {
                        x.Spacing(20);
                        x.Item().Text($"Contract Number: CT-{loan.LoanAccountNo}");
                        x.Item().Text($"Borrower Name: {loan.Customer.FullName}");
                        x.Item().Text($"ID Number: {loan.Customer.IdNumber}");
                        x.Item().Text($"Principal Amount: {loan.ApprovedAmount:N0} VND");
                        x.Item().Text($"Tenor: {loan.TenorMonths} months");
                        x.Item().Text($"Interest Rate: {loan.MonthlyInterestRate}% per month");
                        x.Item().Text($"Electronic Signature Client IP: {ipAddress}");
                        x.Item().Text($"Signed Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                    });
            });
        }).GeneratePdf();

        // 2. Archive PDF (simulated local storage)
        var storageDir = Path.Combine(AppContext.BaseDirectory, ContractStorageRoot, ContractStorageFolder);
        if (!Directory.Exists(storageDir)) Directory.CreateDirectory(storageDir);
        var fileName = $"CT_{loan.LoanAccountNo}_{Guid.NewGuid()}.pdf";
        var storageKey = Path.Combine(ContractStorageFolder, fileName).Replace('\\', '/');
        var filePath = Path.Combine(storageDir, fileName);
        await File.WriteAllBytesAsync(filePath, pdfBytes);

        // 3. Hash Document (SHA-256)
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(pdfBytes);
        var hashString = Convert.ToHexString(hashBytes);

        // 4. Remote CA Digital Signature
        var rsaSignature = await _caProvider.SignHashAsync(hashString);

        var contract = new EContract
        {
            LoanId = loanId,
            ContractNumber = "CT-" + loan.LoanAccountNo,
            PdfContentHash = hashString,
            DigitalSignature = rsaSignature,
            ClientIpAddress = ipAddress,
            PdfStoragePath = storageKey,
            SignedAt = DateTime.UtcNow
        };

        _db.EContracts.Add(contract);

        try
        {
            await _db.SaveChangesAsync(); // save to get ID

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = 0,
                ActionCode = "CONTRACT_SIGNED",
                EntityType = "EContract",
                EntityId = contract.Id,
                NewValuesJson = AuditHelper.ToJson(new { loanId, hash = hashString, storageKey })
            });

            await _db.SaveChangesAsync();
        }
        catch
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            throw;
        }

        return Result.Success(new EContractDto
        {
            Id = contract.Id,
            LoanId = contract.LoanId,
            ContractNumber = contract.ContractNumber,
            PdfContentHash = contract.PdfContentHash,
            DigitalSignature = contract.DigitalSignature,
            ClientIpAddress = contract.ClientIpAddress,
            PdfStoragePath = contract.PdfStoragePath,
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
            DigitalSignature = e.DigitalSignature,
            ClientIpAddress = e.ClientIpAddress,
            PdfStoragePath = e.PdfStoragePath,
            SignedAt = e.SignedAt
        });
    }
}
