using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Assistant.Core.Models.Config
{
    public class NotifyConfig
    {
        public string TeamsWebhookUrl { get; set; } = "";
        public bool TeamsEnabled { get; set; } = true;
        public bool DesktopEnabled { get; set; } = true;
        public AlertSchedule Schedule { get; set; } = new AlertSchedule();
    }

    public class AlertSchedule
    {
        // Dias permitidos: "Mon,Tue,Wed,Thu,Fri"
        public string AllowedDays { get; set; } = "Mon,Tue,Wed,Thu,Fri";
        // Horário início e fim no formato "HH:mm"
        public string StartTime { get; set; } = "08:00";
        public string EndTime { get; set; } = "18:00";

        public bool IsNowAllowed()
        {
            var now = DateTime.Now;

            // Verifica dia da semana
            var dayAbbr = now.DayOfWeek switch
            {
                DayOfWeek.Monday => "Mon",
                DayOfWeek.Tuesday => "Tue",
                DayOfWeek.Wednesday => "Wed",
                DayOfWeek.Thursday => "Thu",
                DayOfWeek.Friday => "Fri",
                DayOfWeek.Saturday => "Sat",
                DayOfWeek.Sunday => "Sun",
                _ => ""
            };
            var allowed = AllowedDays.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(d => d.Trim());
            if (!allowed.Contains(dayAbbr)) return false;

            // Verifica horário
            if (!TimeOnly.TryParse(StartTime, out var start)) start = new TimeOnly(8, 0);
            if (!TimeOnly.TryParse(EndTime, out var end)) end = new TimeOnly(18, 0);

            var nowTime = TimeOnly.FromDateTime(now);
            return nowTime >= start && nowTime <= end;
        }
    }
}