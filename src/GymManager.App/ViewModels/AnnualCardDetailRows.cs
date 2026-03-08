using GymManager.Domain.Entities;

namespace GymManager.App.ViewModels;

public sealed class AnnualCardPauseRecordRow
{
    public AnnualCardPauseRecordRow(AnnualCardPauseRecord record, DateTime today)
    {
        Record = record ?? throw new ArgumentNullException(nameof(record));

        var baseDate = today.Date;
        IsActive = Record.PauseStartDate.Date <= baseDate && Record.ResumeDate.Date > baseDate;
    }

    public AnnualCardPauseRecord Record { get; }

    public bool IsActive { get; }

    public string StatusText => IsActive ? "停卡中" : "已恢复";

    public DateTime PauseStartDate => Record.PauseStartDate;

    public DateTime ResumeDate => Record.ResumeDate;

    public int PauseDays => Record.PauseDays;

    public DateTime EndDateBefore => Record.EndDateBefore;

    public DateTime EndDateAfter => Record.EndDateAfter;

    public DateTime CreatedAt => Record.CreatedAt;

    public string? Note => Record.Note;

    public string PauseRangeText => $"{PauseStartDate:yyyy-MM-dd} → {ResumeDate:yyyy-MM-dd}（{PauseDays}天）";

    public string EndDateChangeText => $"{EndDateBefore:yyyy-MM-dd} → {EndDateAfter:yyyy-MM-dd}（+{PauseDays}天）";
}

public sealed class AnnualCardRenewRecordRow
{
    public AnnualCardRenewRecordRow(AnnualCardRenewRecord record)
    {
        Record = record ?? throw new ArgumentNullException(nameof(record));
        IsRestart = Record.StartDateAfter.Date != Record.StartDateBefore.Date;
    }

    public AnnualCardRenewRecord Record { get; }

    public bool IsRestart { get; }

    public string TypeText => IsRestart ? "重开" : "顺延";

    public DateTime RenewedAt => Record.RenewedAt;

    public DateTime StartDateBefore => Record.StartDateBefore;

    public DateTime EndDateBefore => Record.EndDateBefore;

    public DateTime StartDateAfter => Record.StartDateAfter;

    public DateTime EndDateAfter => Record.EndDateAfter;

    public DateTime CreatedAt => Record.CreatedAt;

    public string? Note => Record.Note;

    public string BeforeRangeText => $"{StartDateBefore:yyyy-MM-dd} ~ {EndDateBefore:yyyy-MM-dd}";

    public string AfterRangeText => $"{StartDateAfter:yyyy-MM-dd} ~ {EndDateAfter:yyyy-MM-dd}";

    public int EndDeltaDays => (EndDateAfter.Date - EndDateBefore.Date).Days;

    public string EndDeltaText
    {
        get
        {
            var delta = EndDeltaDays;
            if (delta == 0)
            {
                return "0天";
            }

            return delta > 0 ? $"+{delta}天" : $"{delta}天";
        }
    }
}

