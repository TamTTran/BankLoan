using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.LoanProduct;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public interface ILoanProductService
{
    Task<IEnumerable<LoanProductDto>> GetAllActiveAsync();
    Task<Result<LoanProductDto>> GetByIdAsync(int id);
    Task<Result<LoanProductDto>> CreateAsync(CreateLoanProductRequest request, int createdByUserId);
    Task<Result<LoanProductDto>> UpdateAsync(int id, UpdateLoanProductRequest request);
    Task<Result<InstallmentPreviewDto>> PreviewInstallmentAsync(InstallmentPreviewRequest request);
}

public class LoanProductService : ILoanProductService
{
    private readonly LoanFlowDbContext _db;

    public LoanProductService(LoanFlowDbContext db) => _db = db;

    public async Task<IEnumerable<LoanProductDto>> GetAllActiveAsync()
    {
        return await _db.LoanProducts
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => MapToDto(p))
            .ToListAsync();
    }

    public async Task<Result<LoanProductDto>> GetByIdAsync(int id)
    {
        var p = await _db.LoanProducts.FindAsync(id);
        if (p is null) return Result.Failure<LoanProductDto>("Sản phẩm vay không tồn tại.");
        return Result.Success(MapToDto(p));
    }

    public async Task<Result<LoanProductDto>> CreateAsync(CreateLoanProductRequest req, int createdByUserId)
    {
        if (await _db.LoanProducts.AnyAsync(p => p.Code == req.Code))
            return Result.Failure<LoanProductDto>("Mã sản phẩm đã tồn tại.");

        if (req.MinAmount >= req.MaxAmount)
            return Result.Failure<LoanProductDto>("Số tiền tối thiểu phải nhỏ hơn tối đa.");

        if (req.MinTenorMonths >= req.MaxTenorMonths)
            return Result.Failure<LoanProductDto>("Kỳ hạn tối thiểu phải nhỏ hơn tối đa.");

        if (req.InterestRatePerMonth <= 0)
            return Result.Failure<LoanProductDto>("Lãi suất phải lớn hơn 0.");

        var entity = new LoanProduct
        {
            Code = req.Code,
            Name = req.Name,
            Description = req.Description,
            MinAmount = req.MinAmount,
            MaxAmount = req.MaxAmount,
            MinTenorMonths = req.MinTenorMonths,
            MaxTenorMonths = req.MaxTenorMonths,
            InterestRatePerMonth = req.InterestRatePerMonth,
            ProcessingFeeRate = req.ProcessingFeeRate,
            RepaymentFrequency = req.RepaymentFrequency,
            CreatedByUserId = createdByUserId
        };
        _db.LoanProducts.Add(entity);
        await _db.SaveChangesAsync();
        return Result.Success(MapToDto(entity));
    }

    public async Task<Result<LoanProductDto>> UpdateAsync(int id, UpdateLoanProductRequest req)
    {
        var entity = await _db.LoanProducts.FindAsync(id);
        if (entity is null) return Result.Failure<LoanProductDto>("Sản phẩm không tồn tại.");

        if (req.MinAmount >= req.MaxAmount)
            return Result.Failure<LoanProductDto>("Số tiền tối thiểu phải nhỏ hơn tối đa.");

        entity.Name = req.Name;
        entity.Description = req.Description;
        entity.MinAmount = req.MinAmount;
        entity.MaxAmount = req.MaxAmount;
        entity.MinTenorMonths = req.MinTenorMonths;
        entity.MaxTenorMonths = req.MaxTenorMonths;
        entity.InterestRatePerMonth = req.InterestRatePerMonth;
        entity.ProcessingFeeRate = req.ProcessingFeeRate;
        entity.IsActive = req.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success(MapToDto(entity));
    }

    public async Task<Result<InstallmentPreviewDto>> PreviewInstallmentAsync(InstallmentPreviewRequest req)
    {
        var product = await _db.LoanProducts.FindAsync(req.LoanProductId);
        if (product is null) return Result.Failure<InstallmentPreviewDto>("Sản phẩm không tồn tại.");

        if (req.Amount < product.MinAmount || req.Amount > product.MaxAmount)
            return Result.Failure<InstallmentPreviewDto>($"Số tiền vay phải từ {product.MinAmount:N0} đến {product.MaxAmount:N0}.");

        if (req.TenorMonths < product.MinTenorMonths || req.TenorMonths > product.MaxTenorMonths)
            return Result.Failure<InstallmentPreviewDto>($"Kỳ hạn phải từ {product.MinTenorMonths} đến {product.MaxTenorMonths} tháng.");

        // Flat-rate installment: (P * r * (1+r)^n) / ((1+r)^n - 1)
        var r = product.InterestRatePerMonth / 100m;
        var n = req.TenorMonths;
        var pow = (decimal)Math.Pow((double)(1 + r), n);
        var monthly = req.Amount * r * pow / (pow - 1);
        var totalRepayment = monthly * n;
        var totalInterest = totalRepayment - req.Amount;
        var processingFee = req.Amount * (product.ProcessingFeeRate ?? 0) / 100m;

        return Result.Success(new InstallmentPreviewDto
        {
            LoanAmount = req.Amount,
            TenorMonths = req.TenorMonths,
            InterestRatePerMonth = product.InterestRatePerMonth,
            MonthlyInstallment = Math.Round(monthly, 0),
            TotalRepayment = Math.Round(totalRepayment, 0),
            TotalInterest = Math.Round(totalInterest, 0),
            ProcessingFee = Math.Round(processingFee, 0)
        });
    }

    private static LoanProductDto MapToDto(LoanProduct p) => new()
    {
        Id = p.Id, Code = p.Code, Name = p.Name, Description = p.Description,
        MinAmount = p.MinAmount, MaxAmount = p.MaxAmount,
        MinTenorMonths = p.MinTenorMonths, MaxTenorMonths = p.MaxTenorMonths,
        InterestRatePerMonth = p.InterestRatePerMonth, ProcessingFeeRate = p.ProcessingFeeRate,
        RepaymentFrequency = p.RepaymentFrequency, IsActive = p.IsActive, CreatedAt = p.CreatedAt
    };
}
