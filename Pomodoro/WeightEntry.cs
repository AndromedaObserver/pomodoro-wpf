using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Pomodoro
{
    public class WeightEntry
    {
        public DateTime Date { get; set; }
        public double Weight { get; set; } // kg
        public string Note { get; set; } = "";

        public string DateDisplay => Date.ToString("MM/dd");
        public string WeightDisplay => $"{Weight:F1} kg";
    }

    public class WeightData
    {
        public double HeightCm { get; set; } // 身高 cm
        public double GoalWeight { get; set; } // 目标体重 kg
        public List<WeightEntry> Entries { get; set; } = new();

        private static string DataPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Pomodoro", "weight.json");

        public static WeightData Load()
        {
            try
            {
                if (File.Exists(DataPath))
                {
                    var json = File.ReadAllText(DataPath);
                    return JsonSerializer.Deserialize<WeightData>(json) ?? new WeightData();
                }
            }
            catch { }
            return new WeightData();
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(DataPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(DataPath, json);
            }
            catch { }
        }

        public double? Bmi => HeightCm > 0 && Entries.Count > 0
            ? Entries.OrderByDescending(e => e.Date).First().Weight / ((HeightCm / 100) * (HeightCm / 100))
            : null;

        public string BmiDisplay => Bmi.HasValue ? $"{Bmi.Value:F1}" : "--";

        public string BmiCategory
        {
            get
            {
                if (!Bmi.HasValue) return "";
                var b = Bmi.Value;
                if (b < 18.5) return "偏瘦";
                if (b < 24) return "正常";
                if (b < 28) return "偏胖";
                return "肥胖";
            }
        }

        public double? WeightChange => Entries.Count >= 2
            ? Entries.OrderByDescending(e => e.Date).First().Weight - Entries.OrderBy(e => e.Date).First().Weight
            : null;

        public string WeightChangeDisplay => WeightChange.HasValue
            ? $"{(WeightChange.Value >= 0 ? "+" : "")}{WeightChange.Value:F1} kg"
            : "--";
    }
}
