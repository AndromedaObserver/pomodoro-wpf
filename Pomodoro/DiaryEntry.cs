using System;
using System.Collections.Generic;

namespace Pomodoro
{
    public class DiaryEntry
    {
        public DateTime Date { get; set; }
        public string Content { get; set; } = "";
        public string MonthGoal { get; set; } = "";
        public string WeekGoal { get; set; } = "";
        public List<DiaryNode> Nodes { get; set; } = new();
    }
}
