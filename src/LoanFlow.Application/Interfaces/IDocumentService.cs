using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IDocumentService
{
    Task<Result<DocumentDto>> UploadAsync(int? loanId, int? customerId, string documentType,
        string fileName, string filePath, long fileSize, string? mimeType, int userId);
    Task<Result> VerifyAsync(int documentId, string status, string? notes, int userId);
    Task<List<DocumentDto>> GetDocumentsAsync(int? loanId, int? customerId);
}
