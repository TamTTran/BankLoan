using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class DocumentDto
{
    public int Id { get; set; }
    public int? LoanId { get; set; }
    public int? CustomerId { get; set; }
    public string DocumentType { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public string Status { get; set; } = "";
    public DateTime UploadedAt { get; set; }
    public string? VerificationNotes { get; set; }
}

public interface IDocumentService
{
    Task<Result<DocumentDto>> UploadAsync(int? loanId, int? customerId, string documentType,
        string fileName, string filePath, long fileSize, string? mimeType, int userId);
    Task<Result> VerifyAsync(int documentId, string status, string? notes, int userId);
    Task<List<DocumentDto>> GetDocumentsAsync(int? loanId, int? customerId);
}

public class DocumentService : IDocumentService
{
    private readonly LoanFlowDbContext _db;

    public DocumentService(LoanFlowDbContext db) => _db = db;

    public async Task<Result<DocumentDto>> UploadAsync(int? loanId, int? customerId, string documentType,
        string fileName, string filePath, long fileSize, string? mimeType, int userId)
    {
        var doc = new LoanDocument
        {
            LoanId = loanId, CustomerId = customerId,
            DocumentType = documentType, FileName = fileName,
            FilePath = filePath, FileSize = fileSize, MimeType = mimeType,
            UploadedByUserId = userId
        };

        _db.Set<LoanDocument>().Add(doc);
        await _db.SaveChangesAsync();

        return Result.Success(new DocumentDto
        {
            Id = doc.Id, LoanId = doc.LoanId, CustomerId = doc.CustomerId,
            DocumentType = doc.DocumentType, FileName = doc.FileName,
            FileSize = doc.FileSize, Status = doc.Status, UploadedAt = doc.UploadedAt
        });
    }

    public async Task<Result> VerifyAsync(int documentId, string status, string? notes, int userId)
    {
        var doc = await _db.Set<LoanDocument>().FindAsync(documentId);
        if (doc == null) return Result.Failure("Document not found.");

        doc.Status = status; // Verified | Rejected
        doc.VerificationNotes = notes;
        doc.VerifiedByUserId = userId;
        doc.VerifiedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<List<DocumentDto>> GetDocumentsAsync(int? loanId, int? customerId)
    {
        var query = _db.Set<LoanDocument>().AsQueryable();
        if (loanId.HasValue) query = query.Where(d => d.LoanId == loanId.Value);
        if (customerId.HasValue) query = query.Where(d => d.CustomerId == customerId.Value);

        return await query.OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentDto
            {
                Id = d.Id, LoanId = d.LoanId, CustomerId = d.CustomerId,
                DocumentType = d.DocumentType, FileName = d.FileName,
                FileSize = d.FileSize, Status = d.Status, UploadedAt = d.UploadedAt,
                VerificationNotes = d.VerificationNotes
            }).ToListAsync();
    }
}
