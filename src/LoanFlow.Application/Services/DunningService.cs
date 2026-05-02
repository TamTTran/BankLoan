using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class DunningService : IDunningService
{
    private readonly LoanFlowDbContext _db;

    public DunningService(LoanFlowDbContext db) => _db = db;

    public async Task<List<DunningStrategyDto>> GetStrategiesAsync()
    {
        return await _db.Set<DunningStrategy>()
            .OrderBy(s => s.DpdThreshold)
            .Select(s => new DunningStrategyDto
            {
                Id = s.Id, DpdThreshold = s.DpdThreshold,
                ActionType = s.ActionType, Description = s.Description, IsActive = s.IsActive
            }).ToListAsync();
    }

    public async Task<DunningStrategyDto> UpsertStrategyAsync(int dpdThreshold, string actionType, string? description, int? templateId)
    {
        var strategy = await _db.Set<DunningStrategy>().FirstOrDefaultAsync(s => s.DpdThreshold == dpdThreshold && s.ActionType == actionType);
        if (strategy == null)
        {
            strategy = new DunningStrategy { DpdThreshold = dpdThreshold, ActionType = actionType };
            _db.Set<DunningStrategy>().Add(strategy);
        }
        strategy.Description = description;
        strategy.NotificationTemplateId = templateId;
        strategy.IsActive = true;
        await _db.SaveChangesAsync();

        return new DunningStrategyDto { Id = strategy.Id, DpdThreshold = strategy.DpdThreshold, ActionType = strategy.ActionType, Description = strategy.Description, IsActive = true };
    }

    public async Task<List<NotificationTemplateDto>> GetTemplatesAsync()
    {
        return await _db.Set<NotificationTemplate>()
            .Select(t => new NotificationTemplateDto
            {
                Id = t.Id, TemplateCode = t.TemplateCode,
                Title = t.Title, Body = t.Body, Channel = t.Channel, IsActive = t.IsActive
            }).ToListAsync();
    }

    public async Task<NotificationTemplateDto> UpsertTemplateAsync(string code, string title, string body, string channel)
    {
        var template = await _db.Set<NotificationTemplate>().FirstOrDefaultAsync(t => t.TemplateCode == code);
        if (template == null)
        {
            template = new NotificationTemplate { TemplateCode = code };
            _db.Set<NotificationTemplate>().Add(template);
        }
        template.Title = title;
        template.Body = body;
        template.Channel = channel;
        template.IsActive = true;
        await _db.SaveChangesAsync();

        return new NotificationTemplateDto { Id = template.Id, TemplateCode = template.TemplateCode, Title = template.Title, Body = template.Body, Channel = template.Channel, IsActive = true };
    }

    public async Task<int> ExecuteDunningAsync(DateOnly businessDate)
    {
        var strategies = await _db.Set<DunningStrategy>()
            .Where(s => s.IsActive)
            .Include(s => s.Template)
            .OrderBy(s => s.DpdThreshold)
            .ToListAsync();

        if (!strategies.Any()) return 0;

        var overdueLoans = await _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.RepaymentSchedules)
            .Where(l => l.Status == LoanStatus.Overdue)
            .ToListAsync();

        int actionsCount = 0;

        foreach (var loan in overdueLoans)
        {
            var unpaid = loan.RepaymentSchedules
                .Where(s => s.Status != InstallmentStatus.Paid && s.DueDate < businessDate)
                .ToList();
            if (!unpaid.Any()) continue;

            var oldestDue = unpaid.Min(s => s.DueDate);
            int dpd = businessDate.DayNumber - oldestDue.DayNumber;

            // Find highest matching strategy
            var matchingStrategy = strategies.Where(s => dpd >= s.DpdThreshold).LastOrDefault();
            if (matchingStrategy?.Template == null) continue;

            // Check if we already sent this strategy's notification today
            var alreadySent = await _db.Notifications
                .AnyAsync(n => n.LoanId == loan.Id
                    && n.Type == $"Dunning_{matchingStrategy.DpdThreshold}"
                    && n.CreatedAt.Date == DateTime.UtcNow.Date);
            if (alreadySent) continue;

            // Render template
            var title = matchingStrategy.Template.Title
                .Replace("{LoanAccountNo}", loan.LoanAccountNo)
                .Replace("{DPD}", dpd.ToString());
            var message = matchingStrategy.Template.Body
                .Replace("{LoanAccountNo}", loan.LoanAccountNo)
                .Replace("{Amount}", loan.OutstandingAmount.ToString("N0"))
                .Replace("{DPD}", dpd.ToString())
                .Replace("{CustomerName}", loan.Customer.FullName);

            _db.Notifications.Add(new Notification
            {
                UserId = loan.Customer.AssignedToUserId,
                LoanId = loan.Id, CustomerId = loan.CustomerId,
                Type = $"Dunning_{matchingStrategy.DpdThreshold}",
                Title = title, Message = message,
                DeeplinkUrl = $"/loans/{loan.Id}"
            });
            actionsCount++;
        }

        if (actionsCount > 0) await _db.SaveChangesAsync();
        return actionsCount;
    }
}
