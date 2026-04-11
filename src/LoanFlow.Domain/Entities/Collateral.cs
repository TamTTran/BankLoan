namespace LoanFlow.Domain.Entities;

/// <summary>
/// Represents a collateral/security asset belonging to a customer.
/// Vietnamese banks (VIB, BIDV, Techcombank) require collateral for secured products:
///   - RealEstate: Sổ đỏ/sổ hồng for mortgage
///   - Vehicle: Đăng ký xe for auto loans
///   - SavingsBook: Sổ tiết kiệm for secured personal loans
/// One collateral can secure multiple loans (via LoanCollateral junction).
/// </summary>
public class Collateral
{
    public int Id { get; set; }
    public int CustomerId { get; set; }

    /// <summary>Type: RealEstate | Vehicle | SavingsBook | OtherAsset</summary>
    public string CollateralType { get; set; } = default!;

    public string Description { get; set; } = default!;         // Mô tả tài sản
    public string? RegistrationNo { get; set; }                  // Số sổ đỏ / biển số xe / số sổ tiết kiệm
    public decimal AppraisedValue { get; set; }                  // Giá trị định giá (VND)
    public DateTime AppraisalDate { get; set; }                  // Ngày định giá
    public string? AppraisedBy { get; set; }                     // Người/đơn vị định giá
    public string Status { get; set; } = "Active";              // Active | Released | Seized
    public string? Address { get; set; }                         // Địa chỉ TSĐB (nếu BĐS)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = default!;
    public ICollection<LoanCollateral> LoanCollaterals { get; set; } = new List<LoanCollateral>();
}
