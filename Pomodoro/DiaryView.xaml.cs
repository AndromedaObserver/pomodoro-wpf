using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using PathIO = System.IO.Path;

namespace Pomodoro
{
    public partial class DiaryView : UserControl
    {
        private DateTime _currentMonth;
        private DateTime? _selectedDate;
        private int _selectedRow = -1, _selectedCol = -1;
        private readonly List<DiaryEntry> _entries = new();
        private readonly string _dataPath;

        // Calendar cell borders
        private readonly Border[,] _cellBorders = new Border[6, 7];

        // Outline editor state
        private readonly List<DiaryNode> _currentNodes = new();
        private readonly Dictionary<int, TextBox> _textBoxes = new();
        private int _focusedIndex = -1;
        private bool _dirty;
        private bool _loading;

        public DiaryView()
        {
            InitializeComponent();
            _dataPath = PathIO.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pomodoro", "diary.json");
            _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            LoadData();
            BuildCalendar();
        }

        #region Data Persistence

        private void LoadData()
        {
            try
            {
                if (File.Exists(_dataPath))
                {
                    var json = File.ReadAllText(_dataPath);
                    var list = JsonSerializer.Deserialize<List<DiaryEntry>>(json);
                    if (list != null) _entries.AddRange(list);
                }
            }
            catch { /* ignore */ }
        }

        private void SaveData()
        {
            try
            {
                var dir = PathIO.GetDirectoryName(_dataPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_dataPath, json);
            }
            catch { /* ignore */ }
        }

        #endregion

        #region Calendar Building

        private void BuildCalendar()
        {
            MonthYearLabel.Text = $"{_currentMonth.Year}年{_currentMonth.Month}月";
            CalendarGrid.Children.Clear();

            int daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);
            int startDow = (int)_currentMonth.DayOfWeek;
            int day = 1;

            for (int row = 0; row < 6; row++)
            {
                for (int col = 0; col < 7; col++)
                {
                    if (row == 0 && col < startDow)
                    {
                        _cellBorders[row, col] = null;
                        continue;
                    }
                    if (day > daysInMonth)
                    {
                        _cellBorders[row, col] = null;
                        continue;
                    }

                    int currentDay = day;
                    var cellDate = new DateTime(_currentMonth.Year, _currentMonth.Month, currentDay);
                    bool isToday = cellDate == DateTime.Today;
                    bool hasEntry = _entries.Any(e => e.Date.Date == cellDate.Date);

                    var border = new Border
                    {
                        CornerRadius = new CornerRadius(4),
                        Margin = new Thickness(1),
                        Background = isToday
                            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0"))
                            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFAFA")),
                        BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8E8E8")),
                        BorderThickness = new Thickness(0.5),
                        Cursor = Cursors.Hand,
                        Tag = cellDate
                    };

                    var stack = new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };

                    var dayText = new TextBlock
                    {
                        Text = currentDay.ToString(),
                        FontSize = 13,
                        FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Foreground = col == 0
                            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CC0000"))
                            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"))
                    };
                    stack.Children.Add(dayText);

                    if (hasEntry)
                    {
                        var dot = new Ellipse
                        {
                            Width = 5, Height = 5,
                            Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BA4949")),
                            Margin = new Thickness(0, 2, 0, 0),
                            HorizontalAlignment = HorizontalAlignment.Center
                        };
                        stack.Children.Add(dot);
                    }

                    border.Child = stack;

                    int r = row, c = col;
                    border.MouseLeftButtonDown += (s, e) => SelectDate(cellDate, r, c);

                    Grid.SetRow(border, row);
                    Grid.SetColumn(border, col);
                    CalendarGrid.Children.Add(border);
                    _cellBorders[row, col] = border;

                    day++;
                }
            }

            // Restore selection if still visible
            if (_selectedDate.HasValue)
            {
                if (_selectedDate.Value.Year == _currentMonth.Year &&
                    _selectedDate.Value.Month == _currentMonth.Month)
                {
                    int selDay = _selectedDate.Value.Day;
                    int selDow = (int)new DateTime(_currentMonth.Year, _currentMonth.Month, 1).DayOfWeek;
                    int index = selDay + selDow - 1;
                    int sr = index / 7;
                    int sc = index % 7;
                    SelectDate(_selectedDate.Value, sr, sc);
                }
                else
                {
                    _selectedDate = null;
                    ClearEditor();
                }
            }
        }

        private void SelectDate(DateTime date, int row, int col)
        {
            // Save current outline before switching
            SaveCurrentNodes();

            // Clear previous selection highlight
            if (_selectedRow >= 0 && _selectedCol >= 0 && _cellBorders[_selectedRow, _selectedCol] != null)
            {
                var prevBorder = _cellBorders[_selectedRow, _selectedCol];
                bool prevIsToday = prevBorder.Tag is DateTime d && d.Date == DateTime.Today;
                prevBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8E8E8"));
                prevBorder.BorderThickness = new Thickness(0.5);
                prevBorder.Background = prevIsToday
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFAFA"));
            }

            _selectedDate = date;
            _selectedRow = row;
            _selectedCol = col;

            if (_cellBorders[row, col] != null)
            {
                _cellBorders[row, col].BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BA4949"));
                _cellBorders[row, col].BorderThickness = new Thickness(2);
            }

            SelectedDateText.Text = date.ToString("yyyy年M月d日 dddd");
            LoadEntry(date);
        }

        private void ClearEditor()
        {
            SelectedDateText.Text = "选择日期";
            MonthGoalBox.Text = "";
            WeekGoalBox.Text = "";
            _currentNodes.Clear();
            OutlinePanel.Children.Clear();
            _textBoxes.Clear();
            _focusedIndex = -1;
            EmptyHint.Visibility = Visibility.Visible;
        }

        #endregion

        #region Outline Editor

        private static readonly string[] Bullets = { "●", "○", "▪", "–" };
        private static readonly double[] FontSizes = { 15, 14, 13, 12 };
        private static readonly string[] BulletColors = { "#BA4949", "#C08460", "#8B9DC3", "#999999" };
        private static readonly string[] TextColors = { "#1A1A1A", "#333333", "#555555", "#777777" };

        private void RebuildOutline()
        {
            OutlinePanel.Children.Clear();
            _textBoxes.Clear();

            for (int i = 0; i < _currentNodes.Count; i++)
            {
                var row = CreateOutlineRow(i);
                OutlinePanel.Children.Add(row);
            }

            EmptyHint.Visibility = _currentNodes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // Restore focus
            if (_focusedIndex >= 0 && _focusedIndex < _textBoxes.Count)
            {
                var tb = _textBoxes[_focusedIndex];
                tb.Focus();
                tb.CaretIndex = tb.Text.Length;
            }
        }

        private FrameworkElement CreateOutlineRow(int index)
        {
            var node = _currentNodes[index];
            int level = Math.Clamp(node.Level, 0, 3);

            // Row grid: bullet | textbox, with left margin for indent
            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var bullet = new TextBlock
            {
                Text = Bullets[level],
                FontSize = FontSizes[level],
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(BulletColors[level])),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 8, 0),
                Width = 16,
                TextAlignment = TextAlignment.Center
            };

            var textBox = new TextBox
            {
                Text = node.Content,
                FontSize = FontSizes[level],
                FontWeight = level == 0 ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(TextColors[level])),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Padding = new Thickness(4, 5, 4, 5),
                AcceptsReturn = false,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top,
                Tag = index
            };

            textBox.TextChanged += OutlineTextChanged;
            textBox.PreviewKeyDown += OutlineKeyDown;
            textBox.GotFocus += OutlineGotFocus;
            textBox.MouseDown += (s, e) =>
            {
                if (_focusedIndex == index) return;
                _focusedIndex = index;
            };

            _textBoxes[index] = textBox;

            Grid.SetColumn(bullet, 0);
            Grid.SetColumn(textBox, 1);
            rowGrid.Children.Add(bullet);
            rowGrid.Children.Add(textBox);

            var border = new Border
            {
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(level * 20, 0, 0, 0),
                CornerRadius = new CornerRadius(4),
                Background = Brushes.Transparent,
                Child = rowGrid,
                Tag = index
            };

            // Hover highlight
            border.MouseEnter += (s, e) =>
            {
                if (_focusedIndex != index)
                    border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0"));
            };
            border.MouseLeave += (s, e) =>
            {
                if (_focusedIndex != index)
                    border.Background = Brushes.Transparent;
            };
            // Click row to focus
            border.MouseLeftButtonDown += (s, e) =>
            {
                _focusedIndex = index;
                if (_textBoxes.TryGetValue(index, out var tb))
                {
                    tb.Focus();
                    tb.CaretIndex = tb.Text.Length;
                }
            };

            return border;
        }

        private void OutlineTextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = (TextBox)sender;
            int index = (int)tb.Tag;
            if (index < _currentNodes.Count)
            {
                _currentNodes[index].Content = tb.Text;
                _dirty = true;
            }
        }

        private void OutlineGotFocus(object sender, RoutedEventArgs e)
        {
            var tb = (TextBox)sender;
            _focusedIndex = (int)tb.Tag;
            // Highlight current row
            for (int i = 0; i < OutlinePanel.Children.Count; i++)
            {
                if (OutlinePanel.Children[i] is Border border)
                {
                    int idx = (int)border.Tag;
                    if (idx == _focusedIndex)
                        border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F4FD"));
                    else
                        border.Background = Brushes.Transparent;
                }
            }
        }

        private void OutlineKeyDown(object sender, KeyEventArgs e)
        {
            var tb = (TextBox)sender;
            int index = (int)tb.Tag;

            switch (e.Key)
            {
                case Key.Enter:
                    e.Handled = true;
                    InsertNodeAfter(index);
                    break;

                case Key.Tab:
                    e.Handled = true;
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                        OutdentNode(index);
                    else
                        IndentNode(index);
                    break;

                case Key.Up:
                    if (index > 0)
                    {
                        e.Handled = true;
                        _focusedIndex = index - 1;
                        RebuildOutline();
                    }
                    break;

                case Key.Down:
                    if (index < _currentNodes.Count - 1)
                    {
                        e.Handled = true;
                        _focusedIndex = index + 1;
                        RebuildOutline();
                    }
                    break;

                case Key.Back:
                    if (string.IsNullOrEmpty(tb.Text) && _currentNodes.Count > 1)
                    {
                        e.Handled = true;
                        DeleteNode(index);
                    }
                    break;

                case Key.Delete:
                    if (string.IsNullOrEmpty(tb.Text) && _currentNodes.Count > 1)
                    {
                        e.Handled = true;
                        DeleteNode(index);
                    }
                    break;
            }
        }

        private void InsertNodeAfter(int index)
        {
            int level = _currentNodes[index].Level;
            _currentNodes.Insert(index + 1, new DiaryNode { Level = level });
            _focusedIndex = index + 1;
            _dirty = true;
            RebuildOutline();
        }

        private void IndentNode(int index)
        {
            if (_currentNodes[index].Level < 3)
            {
                _currentNodes[index].Level++;
                _focusedIndex = index;
                _dirty = true;
                RebuildOutline();
            }
        }

        private void OutdentNode(int index)
        {
            if (_currentNodes[index].Level > 0)
            {
                _currentNodes[index].Level--;
                _focusedIndex = index;
                _dirty = true;
                RebuildOutline();
            }
        }

        private void DeleteNode(int index)
        {
            _currentNodes.RemoveAt(index);
            _focusedIndex = Math.Min(index, _currentNodes.Count - 1);
            _dirty = true;
            RebuildOutline();
        }

        private void SaveCurrentNodes()
        {
            if (!_selectedDate.HasValue || !_dirty) return;

            // Remove trailing empty nodes (keep at least one)
            while (_currentNodes.Count > 1 &&
                   string.IsNullOrWhiteSpace(_currentNodes.Last().Content))
                _currentNodes.RemoveAt(_currentNodes.Count - 1);

            var entry = _entries.FirstOrDefault(e => e.Date.Date == _selectedDate.Value.Date);
            if (entry != null)
            {
                entry.Nodes = _currentNodes.Select(n => new DiaryNode { Content = n.Content, Level = n.Level }).ToList();
                entry.MonthGoal = MonthGoalBox.Text;
                entry.WeekGoal = WeekGoalBox.Text;
            }
            else if (_currentNodes.Any(n => !string.IsNullOrWhiteSpace(n.Content)))
            {
                _entries.Add(new DiaryEntry
                {
                    Date = _selectedDate.Value,
                    Nodes = _currentNodes.Select(n => new DiaryNode { Content = n.Content, Level = n.Level }).ToList(),
                    MonthGoal = MonthGoalBox.Text,
                    WeekGoal = WeekGoalBox.Text
                });
            }
            _dirty = false;
        }

        private void LoadEntry(DateTime date)
        {
            _loading = true;
            var entry = _entries.FirstOrDefault(e => e.Date.Date == date.Date);

            _currentNodes.Clear();
            if (entry != null)
            {
                // Migrate old plain-text content
                if (entry.Nodes.Count == 0 && !string.IsNullOrEmpty(entry.Content))
                    MigrateContent(entry);

                if (entry.Nodes.Count > 0)
                {
                    _currentNodes.AddRange(entry.Nodes.Select(n =>
                        new DiaryNode { Content = n.Content, Level = n.Level }));
                }
                MonthGoalBox.Text = entry.MonthGoal;
                WeekGoalBox.Text = entry.WeekGoal;
            }
            else
            {
                MonthGoalBox.Text = "";
                WeekGoalBox.Text = "";
            }

            // Always have at least one empty node to start typing
            if (_currentNodes.Count == 0)
                _currentNodes.Add(new DiaryNode { Level = 0 });

            _focusedIndex = 0;
            _dirty = false;
            RebuildOutline();
            _loading = false;
        }

        private void MigrateContent(DiaryEntry entry)
        {
            var lines = entry.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    entry.Nodes.Add(new DiaryNode { Content = trimmed, Level = 0 });
            }
        }

        #endregion

        #region Event Handlers

        private void PrevMonthBtn_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(-1);
            BuildCalendar();
        }

        private void NextMonthBtn_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(1);
            BuildCalendar();
        }

        private void PrevDayBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedDate.HasValue) return;
            NavigateToDate(_selectedDate.Value.AddDays(-1));
        }

        private void NextDayBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedDate.HasValue) return;
            NavigateToDate(_selectedDate.Value.AddDays(1));
        }

        private void TodayBtn_Click(object sender, RoutedEventArgs e)
            => NavigateToDate(DateTime.Today);

        private void NavigateToDate(DateTime date)
        {
            if (date.Month != _currentMonth.Month || date.Year != _currentMonth.Year)
            {
                _currentMonth = new DateTime(date.Year, date.Month, 1);
                BuildCalendar();
            }
            int startDow = (int)new DateTime(date.Year, date.Month, 1).DayOfWeek;
            int index = date.Day + startDow - 1;
            int r = index / 7, c = index % 7;
            SelectDate(date, r, c);
        }

        private void SaveDiaryBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedDate.HasValue) return;

            // Remove trailing empty nodes
            while (_currentNodes.Count > 1 &&
                   string.IsNullOrWhiteSpace(_currentNodes.Last().Content))
                _currentNodes.RemoveAt(_currentNodes.Count - 1);

            var entry = _entries.FirstOrDefault(en => en.Date.Date == _selectedDate.Value.Date);
            if (entry != null)
            {
                entry.Nodes = _currentNodes.Select(n => new DiaryNode { Content = n.Content, Level = n.Level }).ToList();
                entry.MonthGoal = MonthGoalBox.Text;
                entry.WeekGoal = WeekGoalBox.Text;
            }
            else
            {
                _entries.Add(new DiaryEntry
                {
                    Date = _selectedDate.Value,
                    Nodes = _currentNodes.Select(n => new DiaryNode { Content = n.Content, Level = n.Level }).ToList(),
                    MonthGoal = MonthGoalBox.Text,
                    WeekGoal = WeekGoalBox.Text
                });
            }

            // Sync goals
            var cal = System.Globalization.CultureInfo.CurrentCulture.Calendar;
            int thisWeek = cal.GetWeekOfYear(_selectedDate.Value,
                System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
            foreach (var ent in _entries)
            {
                if (ent.Date.Year == _selectedDate.Value.Year &&
                    ent.Date.Month == _selectedDate.Value.Month)
                    ent.MonthGoal = MonthGoalBox.Text;
                int ew = cal.GetWeekOfYear(ent.Date,
                    System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
                if (ent.Date.Year == _selectedDate.Value.Year && ew == thisWeek)
                    ent.WeekGoal = WeekGoalBox.Text;
            }

            _dirty = false;
            SaveData();
            BuildCalendar();
            SelectDate(_selectedDate.Value, _selectedRow, _selectedCol);
        }

        private void MonthGoalBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
        }

        private void WeekGoalBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
        }

        #endregion
    }
}
