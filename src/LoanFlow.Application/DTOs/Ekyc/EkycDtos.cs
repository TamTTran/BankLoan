using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

public class EkycResultDto
{
    public string? FullNameExtracted { get; set; }
    public decimal FaceMatchScore { get; set; }
    public decimal OcrConfidence { get; set; }
    public decimal LivenessScore { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RawJson { get; set; }
}

public class EkycVerificationDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime VerificationDate { get; set; }
    public string IdType { get; set; } = "";
    public string? FullNameExtracted { get; set; }
    public decimal? FaceMatchScore { get; set; }
    public decimal? OcrConfidence { get; set; }
    public decimal? LivenessScore { get; set; }
    public string Status { get; set; } = "";
    public string Provider { get; set; } = "";
}
