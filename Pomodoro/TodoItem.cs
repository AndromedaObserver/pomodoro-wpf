using System;

namespace Pomodoro
{
    public class TodoItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "";
        public bool IsDone { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Category { get; set; } = "默认";
        public string ColorHex { get; set; } = "#AAAAAA";
    }
}
