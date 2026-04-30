using System;

namespace OneGood.Core.Models
{
    public class AnalyticsEvent
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string EventType { get; set; } = string.Empty; // e.g. "visit", "action"
        public string? UserAgent { get; set; }
        public string? IpAddress { get; set; }
        public string? BrowserLanguage { get; set; }
        public string? ActionDetail { get; set; } // e.g. action name or category
        public int? UserId { get; set; } // null for anonymous
    }
}