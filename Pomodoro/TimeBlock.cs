using System;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace Pomodoro
{
    public class TimeBlock
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string ColorHex { get; set; } = "#4A90D9";
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        [JsonIgnore]
        public TimeSpan Duration =>
            EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;

        [JsonIgnore]
        public string DurationDisplay
        {
            get
            {
                var d = EndTime.HasValue
                    ? EndTime.Value - StartTime
                    : DateTime.Now - StartTime;
                return $"{(int)d.TotalHours:D2}:{d.Minutes:D2}:{d.Seconds:D2}";
            }
        }

        [JsonIgnore]
        public SolidColorBrush ColorBrush =>
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(ColorHex));

        [JsonIgnore]
        public string TimeRangeDisplay =>
            $"{StartTime:HH:mm} - {EndTime?.ToString("HH:mm") ?? "进行中"}";
    }
}
