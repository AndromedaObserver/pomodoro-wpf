using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.IO;
using System.Text.Json;
using OxyPlot;
using OxyPlot.Wpf;
using OxyPlot.Series;
// using OxyPlot.Axes; — removed: conflicts with System.Windows.VerticalAlignment

namespace Pomodoro
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Declaration
        private string currentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        private static readonly string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pomodoro", "config.json");

        private TimeSpan pomodoroTime = TimeSpan.FromMinutes(25);
        private TimeSpan shortBreakTime = TimeSpan.FromMinutes(5);
        private TimeSpan longBreakTime = TimeSpan.FromMinutes(15);
        private int cyclesNumber = 4;
        private int pomodoroMinutes = 25;
        private int shortBreakMinutes = 5;
        private int longBreakMinutes = 15;
        private bool isMuted = false;
        private int pomodoroCompleted = 0;

        // Phase 1 new configs
        private bool isInfiniteFocus = false;
        private bool autoStartNext = false;

        // Sound config
        private string ambientSoundKey = "";
        private string alarmSoundKey = "classic";
        private SoundPlayer ambientPlayer;
        private string ambientTempFilePath;
        private bool isAmbientPlaying;

        private WaveOutEvent outputDevice;
        private AudioFileReader audioFile;

        private int volume = 100;
        private string state;

        // Todo module
        private List<TodoItem> _todoItems = new();
        private static readonly string TodosPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pomodoro", "todos.json");
        private bool isTimerRunning;
        private TimeSpan remainingTime;

        // Time block module
        private List<TimeBlock> _timeBlocks = new();
        private static readonly string TimeBlocksPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pomodoro", "timeblocks.json");
        private DispatcherTimer _tbTickTimer;

        // Weight module
        private WeightData _weightData;
        private PlotView _weightChart;
        private List<Border> _hourCells = new(48);
        private Dictionary<Border, TimeBlock> _cellBlockMap = new();
        private HashSet<Border> _selectedCells = new();
        private Border _selectionAnchor = null;
        private bool _isDragging = false;
        private bool _dragAddMode = false;
        private Border _dragStartCell = null;

        private static readonly string[] TodoPalette = {
            "#7BAEE4", "#6FCF8F", "#F09090", "#B382C9", "#F7C04A",
            "#7DCFE8", "#F9EC5E", "#BEBEBE", "#F0A0B0", "#66B8B0",
            "#F5B895", "#D0A0C8"
        };
        private static readonly (string Name, string Color, string Icon)[] TimeBlockPresets = {
            ("工作", "#7BAEE4", "💼"),
            ("学习", "#6FCF8F", "📚"),
            ("运动", "#F09090", "🏃"),
            ("阅读", "#B382C9", "📖"),
            ("休息", "#F7C04A", "☕"),
            ("冥想", "#7DCFE8", "🧘"),
            ("吃饭", "#F9EC5E", "🍽️"),
            ("杂事", "#BEBEBE", "📋"),
            ("娱乐", "#F0A0B0", "🎮"),
            ("出行", "#66B8B0", "🚗"),
            ("社交", "#F5B895", "💬"),
            ("创作", "#D0A0C8", "🎨"),
            ("睡觉", "#5B6ABF", "😴"),
        };

        // Alarm state
        private bool isAlarmRinging;
        private SoundPlayer alarmPlayer;

        private DispatcherTimer timer;
        public event PropertyChangedEventHandler PropertyChanged;
        private SolidColorBrush mainColorBrush;
        private SolidColorBrush secondaryColorBrush;
        private SolidColorBrush thirdColorBrush;
        private SolidColorBrush borderColorBrush;
        private SolidColorBrush transparentColorBrush;

        private SolidColorBrush mainColorDarkBrush;
        private SolidColorBrush secondaryColorDarkBrush;
        private SolidColorBrush thirdColorDarkBrush;
        private SolidColorBrush borderColorDarkBrush;
        private SolidColorBrush transparentColorDarkBrush;

        private SolidColorBrush mainColorDarkerBrush;
        private SolidColorBrush secondaryColorDarkerBrush;
        private SolidColorBrush thirdColorDarkerBrush;
        private SolidColorBrush borderColorDarkerBrush;
        #endregion

        #region Initialisation
        public MainWindow()
        {
            InitializeComponent();
            StateChanged += MainWindow_StateChanged;
            InitializeTimer();
            InitializeData();
            InitializeColors();
            InitializeVersion();
            ApplyAdaptiveSize();
            LoadConfig();
            LoadTodos();
            InitTimeBlockPresets();
            InitTimeBlockTimer();
            Build24HourGrid();
            LoadTimeBlocks();
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            UpdateMaximizeButtonIcon();
        }

        /// <summary>
        /// Adaptive sizing: scale window to ~60% screen width, ~70% screen height on first launch.
        /// Config-stored size overrides this via LoadConfig().
        /// </summary>
        private void ApplyAdaptiveSize()
        {
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            // Target: 65% width, 70% height
            double targetWidth = Math.Max(960, Math.Min(screenWidth * 0.65, 1600));
            double targetHeight = Math.Max(580, Math.Min(screenHeight * 0.70, 1200));

            Width = targetWidth;
            Height = targetHeight;
            MinWidth = Math.Min(1100, screenWidth * 0.6);
            MinHeight = Math.Min(700, screenHeight * 0.5);
        }

        private void InitializeData()
        {
            DataContext = this;
            state = "todo";
        }

        private void InitializeTimer()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += new EventHandler(Timer_Tick);
            TimerPomodoroText.Text = pomodoroTime.ToString(@"mm\:ss");
            TimerShortBreakText.Text = shortBreakTime.ToString(@"mm\:ss");
            TimerLongBreakText.Text = longBreakTime.ToString(@"mm\:ss");
            remainingTime = pomodoroTime;
        }

        private void InitializeColors()
        {
            mainColorBrush = (SolidColorBrush)FindResource("mainColor");
            secondaryColorBrush = (SolidColorBrush)FindResource("secondaryColor");
            thirdColorBrush = (SolidColorBrush)FindResource("thirdColor");
            borderColorBrush = (SolidColorBrush)FindResource("borderColor");
            transparentColorBrush = (SolidColorBrush)FindResource("transparentColor");

            mainColorDarkBrush = (SolidColorBrush)FindResource("mainColorDark");
            secondaryColorDarkBrush = (SolidColorBrush)FindResource("secondaryColorDark");
            thirdColorDarkBrush = (SolidColorBrush)FindResource("thirdColorDark");
            borderColorDarkBrush = (SolidColorBrush)FindResource("borderColorDark");

            mainColorDarkerBrush = (SolidColorBrush)FindResource("mainColorDarker");
            secondaryColorDarkerBrush = (SolidColorBrush)FindResource("secondaryColorDarker");
            thirdColorDarkerBrush = (SolidColorBrush)FindResource("thirdColorDarker");
            borderColorDarkerBrush = (SolidColorBrush)FindResource("borderColorDarker");
        }
        #endregion

        #region Update
        private void InitializeVersion()
        {
            VersionArea.Text = currentVersion;
            Console.WriteLine("Pomodoro version : " + currentVersion);
        }
        #endregion

        #region Timer Functions
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (isTimerRunning && !isAlarmRinging)
            {
                remainingTime = remainingTime.Subtract(TimeSpan.FromSeconds(1));
                ResetTimerText();
                if (remainingTime == TimeSpan.Zero)
                {
                    // Alarm: start looping sound, show stop button
                    StartAlarm();
                }
            }
        }

        private void StartAlarm()
        {
            PauseTimer();
            StopAmbientSound();

            if (!isMuted)
            {
                byte[] alarmWav = SoundGenerator.GetAlarmSound(alarmSoundKey);
                if (alarmWav != null)
                {
                    // Save to temp file — PlayLooping with file path is reliable
                    string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Pomodoro");
                    System.IO.Directory.CreateDirectory(tempDir);
                    string alarmTemp = System.IO.Path.Combine(tempDir, "alarm.wav");
                    System.IO.File.WriteAllBytes(alarmTemp, alarmWav);
                    alarmPlayer = new SoundPlayer(alarmTemp);
                }
                else
                {
                    alarmPlayer = new SoundPlayer(Properties.Resources.end);
                }
                alarmPlayer.PlayLooping();
            }

            isAlarmRinging = true;
            AlarmStopRow.Visibility = Visibility.Visible;
            UpdateProgressDisplay();
        }

        #region Ambient Sound
        private void StartAmbientSound()
        {
            string log = $"[{DateTime.Now:HH:mm:ss}] StartAmbientSound: isMuted={isMuted} key=[{ambientSoundKey}] state=[{state}]";
            System.IO.File.AppendAllText(@"D:\debug_ambient.log", log + Environment.NewLine);

            if (isMuted || string.IsNullOrEmpty(ambientSoundKey))
            {
                System.IO.File.AppendAllText(@"D:\debug_ambient.log", $"[{DateTime.Now:HH:mm:ss}] SKIP\n");
                return;
            }

            byte[] wav = SoundGenerator.GetAmbientSound(ambientSoundKey);
            System.IO.File.AppendAllText(@"D:\debug_ambient.log", $"[{DateTime.Now:HH:mm:ss}] WAV={(wav == null ? "null" : wav.Length + " bytes")}\n");
            if (wav == null) return;

            try
            {
                StopAmbientSound();

                string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Pomodoro");
                System.IO.Directory.CreateDirectory(tempDir);
                ambientTempFilePath = System.IO.Path.Combine(tempDir, "ambient.wav");
                System.IO.File.WriteAllBytes(ambientTempFilePath, wav);
                System.IO.File.AppendAllText(@"D:\debug_ambient.log", $"[{DateTime.Now:HH:mm:ss}] Saved to {ambientTempFilePath}, exists={System.IO.File.Exists(ambientTempFilePath)}\n");

                ambientPlayer = new SoundPlayer(ambientTempFilePath);
                ambientPlayer.PlayLooping();
                isAmbientPlaying = true;
                System.IO.File.AppendAllText(@"D:\debug_ambient.log", $"[{DateTime.Now:HH:mm:ss}] PlayLooping OK\n");
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(@"D:\debug_ambient.log", $"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}\n");
            }
        }

        private void StopAmbientSound()
        {
            if (ambientPlayer != null)
            {
                try
                {
                    ambientPlayer.Stop();
                    ambientPlayer.Dispose();
                }
                catch { }
                ambientPlayer = null;
            }

            // Clean up temp file
            if (ambientTempFilePath != null)
            {
                try { System.IO.File.Delete(ambientTempFilePath); } catch { }
                ambientTempFilePath = null;
            }

            isAmbientPlaying = false;
        }
        #endregion

        private void StopAlarmSound()
        {
            if (alarmPlayer != null)
            {
                alarmPlayer.Stop();
                alarmPlayer.Dispose();
                alarmPlayer = null;
            }
            isAlarmRinging = false;
            AlarmStopRow.Visibility = Visibility.Collapsed;
        }

        private void StopAlarmButton_Click(object sender, RoutedEventArgs e)
        {
            StopAlarmSound();
            TransitionAfterTimer();
        }

        /// <summary>
        /// Determine and transition to the next mode after a timer finishes.
        /// </summary>
        private void TransitionAfterTimer()
        {
            if (state == "pomodoro")
            {
                pomodoroCompleted++;
                if (isInfiniteFocus)
                {
                    // Stay in focus mode, restart cycle
                    ChangeState("pomodoro");
                }
                else
                {
                    if (pomodoroCompleted >= cyclesNumber)
                        ChangeState("longbreak");
                    else
                        ChangeState("shortbreak");
                }
            }
            else // shortbreak or longbreak -> back to focus
            {
                ChangeState("pomodoro");
            }

            if (autoStartNext)
                StartTimer();
        }

        private void ChangeState(string stateToGo)
        {
            // Stop alarm if it's ringing
            if (isAlarmRinging)
                StopAlarmSound();

            switch (stateToGo)
            {
                case "pomodoro":
                    state = "pomodoro";
                    pomodoroTime = TimeSpan.FromMinutes(pomodoroMinutes);
                    remainingTime = pomodoroTime;
                    TimerPomodoroText.Text = remainingTime.ToString(@"mm\:ss");
                    TimerPomodoro.Visibility = Visibility.Visible;
                    TimerShortBreak.Visibility = Visibility.Hidden;
                    TimerLongBreak.Visibility = Visibility.Hidden;
                    ///change background
                    Border1.Background = mainColorDarkerBrush;
                    Border2.Background = mainColorBrush;
                    Border3.Background = mainColorDarkBrush;
                    Border4.Background = Brushes.Transparent;
                    Border5.Background = Brushes.Transparent;
                    break;
                case "shortbreak":
                    state = "shortbreak";
                    shortBreakTime = TimeSpan.FromMinutes(shortBreakMinutes);
                    remainingTime = shortBreakTime;
                    TimerShortBreakText.Text = remainingTime.ToString(@"mm\:ss");
                    TimerShortBreak.Visibility = Visibility.Visible;
                    TimerPomodoro.Visibility = Visibility.Hidden;
                    TimerLongBreak.Visibility = Visibility.Hidden;
                    ///change background
                    Border1.Background = secondaryColorDarkerBrush;
                    Border2.Background = secondaryColorBrush;
                    Border3.Background = Brushes.Transparent;
                    Border4.Background = secondaryColorDarkBrush;
                    Border5.Background = Brushes.Transparent;
                    break;
                case "longbreak":
                    state = "longbreak";
                    longBreakTime = TimeSpan.FromMinutes(longBreakMinutes);
                    remainingTime = longBreakTime;
                    TimerLongBreakText.Text = remainingTime.ToString(@"mm\:ss");
                    TimerLongBreak.Visibility = Visibility.Visible;
                    TimerPomodoro.Visibility = Visibility.Hidden;
                    TimerShortBreak.Visibility = Visibility.Hidden;
                    ///change background
                    Border1.Background = thirdColorDarkerBrush;
                    Border2.Background = thirdColorBrush;
                    Border3.Background = Brushes.Transparent;
                    Border4.Background = Brushes.Transparent;
                    Border5.Background = thirdColorDarkBrush;
                    break;
            }
            StopTimer();
            UpdateProgressDisplay();
        }

        private void StartTimer()
        {
            if (isAlarmRinging)
                return;

            timer.Start();
            isTimerRunning = true;
            playPausePlayerButton.IsChecked = true;
            PlaySound(Properties.Resources.start);
            System.IO.File.AppendAllText(@"D:\debug_ambient.log", $"[{DateTime.Now:HH:mm:ss}] StartTimer: key=[{ambientSoundKey}] state=[{state}]\n");
            StartAmbientSound();
        }

        private void PlaySound(System.IO.UnmanagedMemoryStream sound)
        {
            if (!isMuted)
            {
                SoundPlayer soundstart = new SoundPlayer(sound);
                soundstart.Play();
            }
        }

        private void PauseTimer()
        {
            isTimerRunning = false;
            timer.Stop();
            StopAmbientSound();
        }

        private void StopTimer()
        {
            isTimerRunning = false;
            playPausePlayerButton.IsChecked = false;
            timer.Stop();
            StopAmbientSound();
            ResetTimerText();
        }

        private void ResetTimerText()
        {
            if (isAlarmRinging)
                return;

            if (isTimerRunning)
            {
                TimerPomodoroText.Text = remainingTime.ToString(@"mm\:ss");
                TimerLongBreakText.Text = remainingTime.ToString(@"mm\:ss");
                TimerShortBreakText.Text = remainingTime.ToString(@"mm\:ss");
            }
            if (!isTimerRunning)
            {
                switch (state)
                {
                    case "pomodoro":
                        TimerPomodoroText.Text = pomodoroTime.ToString(@"mm\:ss");
                        remainingTime = pomodoroTime;
                        break;
                    case "longbreak":
                        TimerLongBreakText.Text = longBreakTime.ToString(@"mm\:ss");
                        remainingTime = longBreakTime;
                        break;
                    case "shortbreak":
                        TimerShortBreakText.Text = shortBreakTime.ToString(@"mm\:ss");
                        remainingTime = shortBreakTime;
                        break;
                }
            }
        }

        private void UpdateProgressDisplay()
        {
            // Update progress labels across all timer panels
            string progressText = GetProgressLabel();
            string nextText = GetNextTimerLabel();

            ProgressLabel.Text = progressText;
            NextTimerPreview.Text = nextText;
            ProgressLabelShort.Text = progressText;
            NextTimerPreviewShort.Text = nextText;
            ProgressLabelLong.Text = progressText;
            NextTimerPreviewLong.Text = nextText;
        }

        private string GetProgressLabel()
        {
            if (isAlarmRinging)
            {
                if (state == "pomodoro")
                    return $"专注 {pomodoroCompleted + 1}/{cyclesNumber} ✓ 已完成";
                else
                    return "休息结束 ✓";
            }

            switch (state)
            {
                case "pomodoro":
                    return $"专注 {pomodoroCompleted + 1}/{cyclesNumber}";
                case "shortbreak":
                    return $"短休息";
                case "longbreak":
                    return $"长休息";
            }
            return "";
        }

        private string GetNextTimerLabel()
        {
            if (isAlarmRinging)
                return "点击停止铃声继续";

            int totalMins;
            string modeName;

            if (state == "pomodoro")
            {
                int nextCompleted = pomodoroCompleted + 1;
                if (isInfiniteFocus)
                {
                    modeName = "专注";
                    totalMins = pomodoroMinutes;
                }
                else if (nextCompleted >= cyclesNumber)
                {
                    modeName = "长休息";
                    totalMins = longBreakMinutes;
                }
                else
                {
                    modeName = "短休息";
                    totalMins = shortBreakMinutes;
                }
            }
            else
            {
                modeName = "专注";
                totalMins = pomodoroMinutes;
            }

            string label = $"→ {modeName} {totalMins}分钟";

            // If auto-start will fire
            if (autoStartNext && !isAlarmRinging)
                label += " (自动)";

            return label;
        }
        #endregion

        #region Button Click Functions
        private void PomodoroMode_Click(object sender, RoutedEventArgs e)
        {
            if (isAlarmRinging) StopAlarmSound();
            ChangeState("pomodoro");
        }

        private void ShortBreakMode_Click(object sender, RoutedEventArgs e)
        {
            if (isAlarmRinging) StopAlarmSound();
            ChangeState("shortbreak");
        }

        private void LongBreakMode_Click(object sender, RoutedEventArgs e)
        {
            if (isAlarmRinging) StopAlarmSound();
            ChangeState("longbreak");
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            if (isAlarmRinging)
            {
                // Stop alarm + do transition (same as stop button)
                StopAlarmButton_Click(sender, e);
                return;
            }

            // Skip current timer: go to next mode
            bool wasRunning = isTimerRunning;
            StopTimer();
            TransitionAfterTimer();
        }

        private void OpenGitHubPage(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://github.com/Boutzi") { UseShellExecute = true });
            }
            catch { }
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(pomodoroMinutes, shortBreakMinutes, longBreakMinutes, cyclesNumber, volume, isMuted, isInfiniteFocus, autoStartNext, Width, Height, ambientSoundKey, alarmSoundKey);
            if (win.ShowDialog() == true)
            {
                pomodoroMinutes = win.PomodoroMinutes;
                shortBreakMinutes = win.ShortBreakMinutes;
                longBreakMinutes = win.LongBreakMinutes;
                cyclesNumber = win.Cycles;
                volume = win.Volume;
                isMuted = win.IsMuted;
                isInfiniteFocus = win.IsInfiniteFocus;
                autoStartNext = win.AutoStartNext;
                Width = win.WindowWidth;
                Height = win.WindowHeight;

                // Sound settings
                System.IO.File.AppendAllText(@"D:\debug_ambient.log", $"[{DateTime.Now:HH:mm:ss}] Settings: old=[{ambientSoundKey}] new=[{win.AmbientSoundKey}] running={isTimerRunning} state=[{state}]\n");
                StopAmbientSound();
                ambientSoundKey = win.AmbientSoundKey;
                alarmSoundKey = win.AlarmSoundKey;
                if (isTimerRunning) StartAmbientSound();
                System.IO.File.AppendAllText(@"D:\debug_ambient.log", $"[{DateTime.Now:HH:mm:ss}] Settings applied: ambientKey=[{ambientSoundKey}]\n");

                pomodoroTime = TimeSpan.FromMinutes(pomodoroMinutes);
                shortBreakTime = TimeSpan.FromMinutes(shortBreakMinutes);
                longBreakTime = TimeSpan.FromMinutes(longBreakMinutes);

                if (isAlarmRinging) StopAlarmSound();

                if (state == "pomodoro") ChangeState("pomodoro");
                else if (state == "shortbreak") ChangeState("shortbreak");
                else if (state == "longbreak") ChangeState("longbreak");

                SaveConfig();
            }
        }
        #endregion

        #region Player Functions
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            if (isAlarmRinging)
            {
                playPausePlayerButton.IsChecked = false;
                return;
            }
            StartTimer();
        }

        private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            PauseTimer();
        }

        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            if (isAlarmRinging) return;
            StopTimer();
            remainingTime = remainingTime.Add(TimeSpan.FromSeconds(1));
            StartTimer();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (isAlarmRinging) return;
            StopTimer();
        }
        #endregion

        #region Window Configuration
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void ToggleMaximize()
        {
            if (WindowState == WindowState.Normal)
                WindowState = WindowState.Maximized;
            else
                WindowState = WindowState.Normal;
        }

        private void UpdateMaximizeButtonIcon()
        {
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "☐";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Stop any active time block
            var activeBlock = _timeBlocks.Find(t => t.EndTime == null);
            if (activeBlock != null)
                StopTimeBlock(activeBlock);

            SaveConfig();
            SaveTodos();
            SaveTimeBlocks();
            Close();
        }

        
        private void ResizeGrip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragResize();
        }

        [System.Runtime.InteropServices.DllImport("user32")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private void DragResize()
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            SendMessage(hwnd, 0x112, (IntPtr)0xF008, IntPtr.Zero);
        }

        private void SaveConfig()
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var config = new {
                    pomodoroMinutes, shortBreakMinutes, longBreakMinutes, cyclesNumber,
                    volume, isMuted, w = Width, h = Height,
                    isInfiniteFocus, autoStartNext,
                    ambientSoundKey, alarmSoundKey
                };
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return;
                var json = File.ReadAllText(ConfigPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("pomodoroMinutes", out var p)) pomodoroMinutes = p.GetInt32();
                if (root.TryGetProperty("shortBreakMinutes", out var sb)) shortBreakMinutes = sb.GetInt32();
                if (root.TryGetProperty("longBreakMinutes", out var lb)) longBreakMinutes = lb.GetInt32();
                if (root.TryGetProperty("cyclesNumber", out var cy)) cyclesNumber = cy.GetInt32();
                if (root.TryGetProperty("volume", out var v)) volume = v.GetInt32();
                if (root.TryGetProperty("isMuted", out var m)) isMuted = m.GetBoolean();
                if (root.TryGetProperty("w", out var w)) Width = w.GetDouble();
                if (root.TryGetProperty("h", out var h)) Height = h.GetDouble();
                if (root.TryGetProperty("isInfiniteFocus", out var inf)) isInfiniteFocus = inf.GetBoolean();
                if (root.TryGetProperty("autoStartNext", out var asn)) autoStartNext = asn.GetBoolean();
                if (root.TryGetProperty("ambientSoundKey", out var ask)) ambientSoundKey = ask.GetString() ?? "";
                if (root.TryGetProperty("alarmSoundKey", out var alk)) alarmSoundKey = alk.GetString() ?? "classic";

                pomodoroTime = TimeSpan.FromMinutes(pomodoroMinutes);
                shortBreakTime = TimeSpan.FromMinutes(shortBreakMinutes);
                longBreakTime = TimeSpan.FromMinutes(longBreakMinutes);

                UpdateProgressDisplay();
            }
            catch { }
        }

        private void MouseDragBar(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                // Double-click to toggle maximize
                if (e.ClickCount == 2)
                {
                    ToggleMaximize();
                    return;
                }
                DragMove();
            }
        }
        #endregion

        #region Time Block Functions
        private void InitTimeBlockPresets()
        {
            TimeBlockPresetPanel.Children.Clear();
            foreach (var (name, color, icon) in TimeBlockPresets)
            {
                var btn = new System.Windows.Controls.Button
                {
                    Content = $"{icon} {name}",
                    Width = 80,
                    Height = 34,
                    Margin = new System.Windows.Thickness(0, 0, 6, 6),
                    FontSize = 12,
                    FontWeight = System.Windows.FontWeights.Bold,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    BorderThickness = new System.Windows.Thickness(0),
                    Tag = name,
                };

                var bgColor = (Color)ColorConverter.ConvertFromString(color);
                btn.Background = new SolidColorBrush(Color.FromArgb(30, bgColor.R, bgColor.G, bgColor.B));
                btn.Foreground = new SolidColorBrush(bgColor);
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(80, bgColor.R, bgColor.G, bgColor.B));
                btn.BorderThickness = new System.Windows.Thickness(1);
                btn.Click += TimeBlockPreset_Click;

                TimeBlockPresetPanel.Children.Add(btn);
            }
        }

        private void InitTimeBlockTimer()
        {
            _tbTickTimer = new DispatcherTimer();
            _tbTickTimer.Interval = TimeSpan.FromSeconds(1);
            _tbTickTimer.Tick += (_, _) => UpdateActiveBlockDisplay();
        }

        private void TimeBlockPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string name)
            {
                // If cells are selected, assign the preset to them
                if (_selectedCells.Count > 0)
                {
                    AssignPresetToSelectedCells(name);
                    return;
                }

                // Find the preset data
                var preset = Array.Find(TimeBlockPresets, p => p.Name == name);

                // Check if there's an active block
                var activeBlock = _timeBlocks.Find(t => t.EndTime == null);

                if (activeBlock != null)
                {
                    // If same activity, just stop
                    if (activeBlock.Name == name)
                    {
                        StopTimeBlock(activeBlock);
                        return;
                    }
                    // Different activity: stop current and start new
                    StopTimeBlock(activeBlock);
                }

                // Start new block
                var block = new TimeBlock
                {
                    Name = preset.Name,
                    ColorHex = preset.Color,
                    StartTime = DateTime.Now,
                    EndTime = null,
                };
                _timeBlocks.Add(block);
                SaveTimeBlocks();
                RefreshTimeBlockList();
                RefreshHourGrid();
                ShowActiveBlock(block);
            }
        }

        private void StopTimeBlock(TimeBlock block)
        {
            block.EndTime = DateTime.Now;
            SaveTimeBlocks();
            RefreshTimeBlockList();
            RefreshHourGrid();
            HideActiveBlock();
            UpdateTodayTotal();
        }

        private void StopTimeBlock_Click(object sender, RoutedEventArgs e)
        {
            var activeBlock = _timeBlocks.Find(t => t.EndTime == null);
            if (activeBlock != null)
                StopTimeBlock(activeBlock);
        }

        private void StartTimeBlockAt(string todoTitle, DateTime startTime)
        {
            // Find color from matching category or use default
            string colorHex = "#4A90D9";
            var todo = _todoItems.Find(t => t.Title == todoTitle);
            if (todo != null)
            {
                var preset = Array.Find(TimeBlockPresets, p => p.Name == todo.Category);
                if (preset.Name != null)
                    colorHex = preset.Color;
            }

            // Stop any active block
            var activeBlock = _timeBlocks.Find(t => t.EndTime == null);
            if (activeBlock != null)
                StopTimeBlock(activeBlock);

            var block = new TimeBlock
            {
                Name = todoTitle,
                ColorHex = colorHex,
                StartTime = startTime,
                EndTime = startTime.AddMinutes(60), 
                // 一小时 = 2个格子合并
            };
            _timeBlocks.Add(block);
            SaveTimeBlocks();
            RefreshTimeBlockList();
            RefreshHourGrid();
            ShowActiveBlock(block);
        }

        private void ShowActiveBlock(TimeBlock block)
        {
            ActiveBlockName.Text = $"{GetPresetIcon(block.Name)} {block.Name}";
            ActiveBlockTime.Text = block.DurationDisplay;
            TimeBlockActiveCard.Visibility = Visibility.Visible;
            _tbTickTimer.Start();
        }

        private void HideActiveBlock()
        {
            TimeBlockActiveCard.Visibility = Visibility.Collapsed;
            _tbTickTimer.Stop();
        }

        private string GetPresetIcon(string name)
        {
            foreach (var (n, _, icon) in TimeBlockPresets)
                if (n == name) return icon;
            return "⏱";
        }

        private void UpdateActiveBlockDisplay()
        {
            var activeBlock = _timeBlocks.Find(t => t.EndTime == null);
            if (activeBlock != null)
            {
                ActiveBlockTime.Text = activeBlock.DurationDisplay;
            }
            else
            {
                HideActiveBlock();
            }
        }

        private void UpdateTodayTotal()
        {
            var todayStart = DateTime.Today;
            var total = TimeSpan.Zero;
            foreach (var tb in _timeBlocks)
            {
                if (tb.StartTime >= todayStart && tb.EndTime.HasValue)
                    total += tb.Duration;
            }
            TodayTotalLabel.Text = total > TimeSpan.Zero
                ? $"今日累计 {(int)total.TotalHours:D2}:{total.Minutes:D2}"
                : "今日无记录";
        }

        private void RefreshTimeBlockList()
        {
            // Show only today's completed blocks, most recent first
            var todayStart = DateTime.Today;
            var todayBlocks = _timeBlocks
                .FindAll(t => t.StartTime >= todayStart)
                .OrderByDescending(t => t.StartTime)
                .ToList();

            TimeBlockList.ItemsSource = null;
            TimeBlockList.ItemsSource = todayBlocks;

            TimeBlockEmpty.Visibility = todayBlocks.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;

            UpdateTodayTotal();
            UpdateTimeStatsChart();
        }

        private void LoadTimeBlocks()
        {
            try
            {
                if (!File.Exists(TimeBlocksPath)) return;
                var json = File.ReadAllText(TimeBlocksPath);
                _timeBlocks = JsonSerializer.Deserialize<List<TimeBlock>>(json) ?? new();
                RefreshTimeBlockList();
                RefreshHourGrid();

                // Resume any active block display
                var activeBlock = _timeBlocks.Find(t => t.EndTime == null);
                if (activeBlock != null)
                    ShowActiveBlock(activeBlock);
            }
            catch { _timeBlocks = new(); }
        }

        private void SaveTimeBlocks()
        {
            try
            {
                var dir = Path.GetDirectoryName(TimeBlocksPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(TimeBlocksPath, JsonSerializer.Serialize(_timeBlocks, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        #region 24h Grid Functions
        private void Build24HourGrid()
        {
            _hourCells.Clear();
            Hour24Grid.Children.Clear();
            Hour24Grid.RowDefinitions.Clear();
            Hour24Grid.ColumnDefinitions.Clear();

            // Col 0: time label | Col 1: 0-29min | Col 2: 30-59min
            Hour24Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Hour24Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Hour24Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var now = DateTime.Now;
            var todayStart = DateTime.Today;

            // Build current time indicator row
            Hour24Grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var nowLabel = new System.Windows.Controls.TextBlock
            {
                Text = $"现在 {now:HH:mm}",
                FontSize = 10,
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xBA, 0x49, 0x49)),
                Margin = new System.Windows.Thickness(0, 0, 4, 2),
            };
            Grid.SetRow(nowLabel, 0);
            Grid.SetColumn(nowLabel, 0);
            Hour24Grid.Children.Add(nowLabel);
            var nowBar = new Border
            {
                Height = 3,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromRgb(0xBA, 0x49, 0x49)),
                Margin = new System.Windows.Thickness(1, 0, 1, 2),
            };
            Grid.SetRow(nowBar, 0);
            Grid.SetColumnSpan(nowBar, 2);
            Grid.SetColumn(nowBar, 1);
            Hour24Grid.Children.Add(nowBar);

            for (int h = 0; h < 24; h++)
            {
                Hour24Grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });

                // Time label
                var label = new System.Windows.Controls.TextBlock
                {
                    Text = $"{h:D2}:00",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new System.Windows.Thickness(0, 0, 4, 0),
                    Width = 32,
                };
                Grid.SetRow(label, h + 1);
                Grid.SetColumn(label, 0);
                Hour24Grid.Children.Add(label);

                // Cell 1: h:00 - h:29
                var cell1 = MakeHourCell(h, 0);
                _hourCells.Add(cell1);
                Grid.SetRow(cell1, h + 1);
                Grid.SetColumn(cell1, 1);
                Hour24Grid.Children.Add(cell1);

                // Cell 2: h:30 - h:59
                var cell2 = MakeHourCell(h, 30);
                _hourCells.Add(cell2);
                Grid.SetRow(cell2, h + 1);
                Grid.SetColumn(cell2, 2);
                Hour24Grid.Children.Add(cell2);
            }

            RefreshHourGrid();

            // Drag selection handlers (Preview level, handledEventsToo to survive ScrollViewer)
            Hour24Grid.AddHandler(PreviewMouseMoveEvent, new MouseEventHandler(HourGrid_MouseMove), true);
            Hour24Grid.AddHandler(PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(HourGrid_MouseUp), true);
        }

        private Border MakeHourCell(int hour, int minute)
        {
            var timeText = $"{hour:D2}:{minute:D2}";
            var cell = new Border
            {
                Height = 26,
                Margin = new System.Windows.Thickness(1),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(30, 200, 200, 200)),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, hour, minute, 0),
                ToolTip = timeText,
                Child = new TextBlock
                {
                    Text = timeText,
                    FontSize = 8,
                    FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                    Foreground = new SolidColorBrush(Color.FromArgb(120, 80, 80, 80)),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                },
            };
            cell.AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(HourCell_Click), true);
            return cell;
        }

        private void RefreshHourGrid()
        {
            _cellBlockMap.Clear();
            var todayStart = DateTime.Today;
            var todayEnd = todayStart.AddDays(1);

            var todayBlocks = _timeBlocks
                .Where(t => t.StartTime < todayEnd && (!t.EndTime.HasValue || t.EndTime.Value >= todayStart))
                .ToList();

            foreach (var cell in _hourCells)
            {
                if (cell.Tag is DateTime cellStart)
                {
                    var cellEnd = cellStart.AddMinutes(30);
                    var block = todayBlocks.Find(t =>
                        t.StartTime < cellEnd && (!t.EndTime.HasValue || t.EndTime.Value > cellStart));

                    var tb = cell.Child as TextBlock;
                    if (block != null)
                    {
                        _cellBlockMap[cell] = block;
                        var color = (Color)ColorConverter.ConvertFromString(block.ColorHex);
                        cell.Background = new SolidColorBrush(Color.FromArgb(160, color.R, color.G, color.B));
                        cell.BorderBrush = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B));
                        cell.BorderThickness = new System.Windows.Thickness(1.5);
                        cell.ToolTip = $"{block.Name}: {block.TimeRangeDisplay}";
                        if (tb != null) { tb.Text = block.Name; tb.Foreground = new SolidColorBrush(Color.FromArgb(240, 33, 33, 33)); }
                    }
                    else
                    {
                        _cellBlockMap.Remove(cell);
                        cell.Background = new SolidColorBrush(Color.FromArgb(20, 200, 200, 200));
                        cell.BorderBrush = Brushes.Transparent;
                        cell.BorderThickness = new System.Windows.Thickness(0);
                        cell.ToolTip = $"{cellStart:HH:mm}";
                        if (tb != null) { tb.Text = $"{cellStart:HH:mm}"; tb.Foreground = new SolidColorBrush(Color.FromArgb(120, 80, 80, 80)); }
                    }
                }
            }
        }

        private void HourCell_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (!(sender is Border cell) || !(cell.Tag is DateTime cellTime)) return;

            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift && _selectionAnchor != null)
            {
                SelectRange(_selectionAnchor, cell);
                return;
            }

            // Toggle clicked cell, remember mode for drag continuation
            bool wasSelected = _selectedCells.Contains(cell);
            if (wasSelected)
                _selectedCells.Remove(cell);
            else
                _selectedCells.Add(cell);

            _selectionAnchor = _selectedCells.Count > 0 ? _selectedCells.Last() : null;
            RefreshSelectionVisuals();
            UpdateSelectedCountDisplay();

            // Prepare for potential drag: remember starting cell + mode, but don't capture yet
            // (Mouse.Capture blocks clicks on preset buttons until released)
            _dragAddMode = !wasSelected;
            _dragStartCell = cell;
        }

        private void HourGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { _isDragging = false; _dragStartCell = null; return; }

            // Start dragging on first mouse movement away from the initial cell
            if (!_isDragging && _dragStartCell != null)
            {
                _isDragging = true;
                Mouse.Capture(Hour24Grid, CaptureMode.Element);
            }

            if (!_isDragging) return;

            var pos = e.GetPosition(Hour24Grid);
            var hit = System.Windows.Media.VisualTreeHelper.HitTest(Hour24Grid, pos);
            Border cell = null;
            if (hit?.VisualHit is Border b)
                cell = b;
            else if (hit?.VisualHit is TextBlock tb && tb.Parent is Border parentBorder && _hourCells.Contains(parentBorder))
                cell = parentBorder;

            if (cell == null || !_hourCells.Contains(cell)) return;

            if (_dragAddMode)
            {
                if (_selectedCells.Add(cell))
                {
                    _selectionAnchor = cell;
                    RefreshSelectionVisuals();
                    UpdateSelectedCountDisplay();
                }
            }
            else
            {
                if (_selectedCells.Remove(cell))
                {
                    _selectionAnchor = _selectedCells.Count > 0 ? _selectedCells.Last() : null;
                    RefreshSelectionVisuals();
                    UpdateSelectedCountDisplay();
                }
            }
        }

        private void HourGrid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                _dragStartCell = null;
                Mouse.Capture(null);
                e.Handled = true;
            }
        }

        private void SelectRange(Border startCell, Border endCell)
        {
            if (!(startCell.Tag is DateTime startTime) || !(endCell.Tag is DateTime endTime)) return;
            if (startTime > endTime) { var tmp = startTime; startTime = endTime; endTime = tmp; }

            _selectedCells.Clear();
            foreach (var c in _hourCells)
            {
                if (c.Tag is DateTime ct && ct >= startTime && ct < endTime.AddMinutes(30))
                    _selectedCells.Add(c);
            }
            _selectionAnchor = _selectedCells.Count > 0 ? _selectedCells.Last() : null;
            RefreshSelectionVisuals();
        }



        private void SelectFromNow_Click(object sender, RoutedEventArgs e)
        {
            var now = DateTime.Now;
            var todayEnd = DateTime.Today.AddDays(1).AddMinutes(-1);

            _selectedCells.Clear();
            foreach (var c in _hourCells)
            {
                if (c.Tag is DateTime ct && ct >= now && ct <= todayEnd)
                    _selectedCells.Add(c);
            }
            _selectionAnchor = _selectedCells.Count > 0 ? _selectedCells.Last() : null;
            RefreshSelectionVisuals();
            UpdateSelectedCountDisplay();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            _selectedCells.Clear();
            foreach (var c in _hourCells)
            {
                _selectedCells.Add(c);
            }
            _selectionAnchor = _selectedCells.Count > 0 ? _selectedCells.Last() : null;
            RefreshSelectionVisuals();
            UpdateSelectedCountDisplay();
        }

        private void RefreshSelectionVisuals()
        {
            foreach (var cell in _hourCells)
            {
                bool isSelected = _selectedCells.Contains(cell);
                var tb = cell.Child as TextBlock;

                if (_cellBlockMap.TryGetValue(cell, out var block))
                {
                    var color = (Color)ColorConverter.ConvertFromString(block.ColorHex);
                    if (isSelected)
                    {
                        cell.BorderBrush = new SolidColorBrush(Color.FromArgb(200, 33, 33, 33));
                        cell.BorderThickness = new Thickness(2.5);
                        cell.Background = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B));
                        if (tb != null) tb.Foreground = new SolidColorBrush(Color.FromArgb(240, 33, 33, 33));
                    }
                    else
                    {
                        cell.BorderBrush = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B));
                        cell.BorderThickness = new Thickness(1.5);
                        cell.Background = new SolidColorBrush(Color.FromArgb(160, color.R, color.G, color.B));
                        if (tb != null) tb.Foreground = new SolidColorBrush(Color.FromArgb(240, 33, 33, 33));
                    }
                }
                else
                {
                    if (isSelected)
                    {
                        cell.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 70, 130, 180));
                        cell.BorderThickness = new Thickness(2.5);
                        cell.Background = new SolidColorBrush(Color.FromArgb(200, 70, 130, 180));
                        if (tb != null) tb.Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
                    }
                    else
                    {
                        cell.BorderBrush = Brushes.Transparent;
                        cell.BorderThickness = new Thickness(0);
                        cell.Background = new SolidColorBrush(Color.FromArgb(20, 200, 200, 200));
                        if (tb != null) tb.Foreground = new SolidColorBrush(Color.FromArgb(120, 80, 80, 80));
                    }
                }
            }
        }

        private void ClearSelectedCells()
        {
            if (_selectedCells.Count == 0) return;

            var toRemove = new List<TimeBlock>();
            foreach (var cell in _selectedCells)
            {
                if (_cellBlockMap.TryGetValue(cell, out var block))
                {
                    if (!toRemove.Contains(block))
                        toRemove.Add(block);
                }
            }

            foreach (var block in toRemove)
                _timeBlocks.Remove(block);

            SaveTimeBlocks();
            _selectedCells.Clear();
            _selectionAnchor = null;
            RefreshHourGrid();
            RefreshTimeBlockList();
            UpdateTodayTotal();
            UpdateSelectedCountDisplay();
        }

        private void AssignTodoToSelectedCells(string todoTitle)
        {
            if (_selectedCells.Count == 0 || string.IsNullOrEmpty(todoTitle)) return;

            // Get sorted selected cell times
            var times = _selectedCells
                .Select(c => c.Tag is DateTime dt ? dt : DateTime.MinValue)
                .Where(t => t != DateTime.MinValue)
                .OrderBy(t => t)
                .ToList();
            if (times.Count == 0) return;

            // Group consecutive cells into blocks (contiguous 30-min slots)
            var groups = new List<List<DateTime>>();
            var current = new List<DateTime> { times[0] };
            for (int i = 1; i < times.Count; i++)
            {
                if (times[i] == times[i - 1].AddMinutes(30))
                    current.Add(times[i]);
                else
                {
                    groups.Add(current);
                    current = new List<DateTime> { times[i] };
                }
            }
            groups.Add(current);

            // Determine color
            string colorHex = GetColorForTodo(todoTitle);

            // Create blocks for each contiguous group
            foreach (var group in groups)
            {
                // Check if any slot already has a block
                foreach (var t in group)
                {
                    var existing = _timeBlocks.Find(b => b.StartTime <= t && b.EndTime > t);
                    if (existing != null) _timeBlocks.Remove(existing);
                }

                var block = new TimeBlock
                {
                    Name = todoTitle,
                    ColorHex = colorHex,
                    StartTime = group.First(),
                    EndTime = group.Last().AddMinutes(30),
                };
                _timeBlocks.Add(block);
            }

            SaveTimeBlocks();
            _isDragging = false;
            _dragStartCell = null;
            _selectedCells.Clear();
            _selectionAnchor = null;
            RefreshHourGrid();
            RefreshTimeBlockList();
            UpdateTodayTotal();
            UpdateSelectedCountDisplay();
        }

        private void AssignPresetToSelectedCells(string presetName)
        {
            if (_selectedCells.Count == 0 || string.IsNullOrEmpty(presetName)) return;

            var preset = Array.Find(TimeBlockPresets, p => p.Name == presetName);
            if (preset.Name == null) return;

            var times = _selectedCells
                .Select(c => c.Tag is DateTime dt ? dt : DateTime.MinValue)
                .Where(t => t != DateTime.MinValue)
                .OrderBy(t => t)
                .ToList();
            if (times.Count == 0) return;

            // Group consecutive cells into blocks
            var groups = new List<List<DateTime>>();
            var current = new List<DateTime> { times[0] };
            for (int i = 1; i < times.Count; i++)
            {
                if (times[i] == times[i - 1].AddMinutes(30))
                    current.Add(times[i]);
                else
                {
                    groups.Add(current);
                    current = new List<DateTime> { times[i] };
                }
            }
            groups.Add(current);

            // Create blocks for each contiguous group
            foreach (var group in groups)
            {
                // Check if any slot already has a block
                foreach (var t in group)
                {
                    var existing = _timeBlocks.Find(b => b.StartTime <= t && b.EndTime > t);
                    if (existing != null) _timeBlocks.Remove(existing);
                }

                var block = new TimeBlock
                {
                    Name = preset.Name,
                    ColorHex = preset.Color,
                    StartTime = group.First(),
                    EndTime = group.Last().AddMinutes(30),
                };
                _timeBlocks.Add(block);
            }

            SaveTimeBlocks();
            _isDragging = false;
            _dragStartCell = null;
            _selectedCells.Clear();
            _selectionAnchor = null;
            RefreshHourGrid();
            RefreshTimeBlockList();
            UpdateTodayTotal();
            UpdateSelectedCountDisplay();
        }

        private string GetColorForTodo(string title)
        {
            var preset = Array.Find(TimeBlockPresets, p => p.Name == title);
            if (preset.Name != null) return preset.Color;
            var todo = _todoItems.Find(t => t.Title == title);
            if (todo != null && !string.IsNullOrEmpty(todo.Category))
            {
                var catPreset = Array.Find(TimeBlockPresets, p => p.Name == todo.Category);
                if (catPreset.Name != null) return catPreset.Color;
            }
            int hash = title.GetHashCode() & 0x7FFFFFFF;
            return TodoPalette[hash % TodoPalette.Length];
        }

        private void UpdateSelectedCountDisplay()
        {
            var count = _selectedCells.Count;
            SelectedCountLabel.Text = count > 0 ? $"已选中 {count} 个时间段" : "";
            ClearSelectedBtn.IsEnabled = count > 0;
            SelectionBar.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        #endregion
        #endregion

        #region Todo Functions
        private void SidebarPomodoro_Click(object sender, RoutedEventArgs e)
        {
            SidebarPomodoroBorder.Background = (SolidColorBrush)FindResource("mainColorDark");
            SidebarTodoBorder.Background = Brushes.Transparent;
            SidebarWeightBorder.Background = Brushes.Transparent;
            SidebarDiaryBorder.Background = Brushes.Transparent;
            ModeTabBar.Visibility = Visibility.Visible;
            PomodoroView.Visibility = Visibility.Visible;
            TodoView.Visibility = Visibility.Collapsed;
            WeightView.Visibility = Visibility.Collapsed;
            DiaryView.Visibility = Visibility.Collapsed;
        }

        private void SidebarTodo_Click(object sender, RoutedEventArgs e)
        {
            SidebarTodoBorder.Background = (SolidColorBrush)FindResource("mainColorDark");
            SidebarPomodoroBorder.Background = Brushes.Transparent;
            SidebarWeightBorder.Background = Brushes.Transparent;
            SidebarDiaryBorder.Background = Brushes.Transparent;
            ModeTabBar.Visibility = Visibility.Collapsed;
            PomodoroView.Visibility = Visibility.Collapsed;
            TodoView.Visibility = Visibility.Visible;
            WeightView.Visibility = Visibility.Collapsed;
            DiaryView.Visibility = Visibility.Collapsed;

            // Refresh time block display when navigating to todo
            RefreshTimeBlockList();
            RefreshHourGrid();
        }

        private void AddTodo_Click(object sender, RoutedEventArgs e)
        {
            var title = TodoInput.Text.Trim();
            if (string.IsNullOrEmpty(title)) return;
            var todo = new TodoItem { Title = title };
            todo.ColorHex = GetColorForTodo(title);
            _todoItems.Add(todo);
            TodoInput.Clear();
            RefreshTodoList();
            SaveTodos();
        }

        private void AssignTodoToCells_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string title)
            {
                AssignTodoToSelectedCells(title);
            }
        }

        private void ClearSelected_Click(object sender, RoutedEventArgs e)
        {
            ClearSelectedCells();
        }

        private void TodoInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                AddTodo_Click(sender, e);
        }

        private void DeleteTodo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string id)
            {
                _todoItems.RemoveAll(t => t.Id == id);
                RefreshTodoList();
                SaveTodos();
            }
        }

        private void TodoCheckChanged(object sender, RoutedEventArgs e)
        {
            SaveTodos();
        }

        private void RefreshTodoList()
        {
            TodoListBox.ItemsSource = null;
            TodoListBox.ItemsSource = _todoItems;
        }

        private void UpdateTimeStatsChart()
        {
            TimeStatsCanvas.Children.Clear();
            TimeStatsLegend.Children.Clear();

            var today = DateTime.Today;
            var todayBlocks = _timeBlocks
                .Where(t => t.StartTime.Date == today && t.EndTime.HasValue)
                .ToList();

            if (todayBlocks.Count == 0)
            {
                TimeStatsSummary.Text = "今日暂无记录";
                return;
            }

            // Group by activity (Name + ColorHex) and sum durations
            var groups = todayBlocks
                .GroupBy(t => new { t.Name, t.ColorHex })
                .Select(g => new
                {
                    Name = g.Key.Name,
                    ColorHex = g.Key.ColorHex,
                    TotalSeconds = g.Sum(t => (t.EndTime.Value - t.StartTime).TotalSeconds)
                })
                .OrderByDescending(g => g.TotalSeconds)
                .ToList();

            double totalTrackedSec = groups.Sum(g => g.TotalSeconds);
            double daySec = 24 * 3600;
            double remainingSec = daySec - totalTrackedSec;

            // ---- Draw donut chart ----
            const double cx = 110, cy = 110;
            const double outerR = 100, innerR = 62;
            double currentAngle = -90;

            foreach (var g in groups)
            {
                double sweep = (g.TotalSeconds / daySec) * 360.0;
                if (sweep < 0.1) continue;
                TimeStatsCanvas.Children.Add(MakeArcPath(cx, cy, outerR, innerR, currentAngle, sweep, g.ColorHex));
                currentAngle += sweep;
            }

            // Remaining/untracked time segment
            if (remainingSec > 30)
            {
                double sweep = (remainingSec / daySec) * 360.0;
                if (sweep > 0.1)
                    TimeStatsCanvas.Children.Add(MakeArcPath(cx, cy, outerR, innerR, currentAngle, sweep, "#EAEAEA"));
            }

            // ---- Build summary ----
            TimeStatsSummary.Text = $"已记录 {FormatDuration(totalTrackedSec)} / 24h，剩余 {FormatDuration(remainingSec)}";

            // ---- Build legend ----
            foreach (var g in groups)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };

                row.Children.Add(new Border
                {
                    Width = 12, Height = 12,
                    CornerRadius = new CornerRadius(2),
                    Background = (Brush)new BrushConverter().ConvertFromString(g.ColorHex),
                    Margin = new Thickness(0, 2, 6, 0)
                });

                row.Children.Add(new TextBlock
                {
                    Text = g.Name, FontSize = 13, FontWeight = System.Windows.FontWeights.SemiBold,
                    Foreground = (Brush)new BrushConverter().ConvertFromString("#333333"),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                });

                row.Children.Add(new TextBlock
                {
                    Text = FormatDuration(g.TotalSeconds),
                    FontSize = 12,
                    Foreground = (Brush)new BrushConverter().ConvertFromString("#888888"),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                });

                row.Children.Add(new TextBlock
                {
                    Text = $" ({g.TotalSeconds / daySec * 100:F1}%)",
                    FontSize = 11,
                    Foreground = (Brush)new BrushConverter().ConvertFromString("#AAAAAA"),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 0, 0)
                });

                TimeStatsLegend.Children.Add(row);
            }
        }

        private static System.Windows.Shapes.Path MakeArcPath(double cx, double cy, double outerR, double innerR,
            double startDeg, double sweepDeg, string colorHex)
        {
            double startRad = startDeg * Math.PI / 180;
            double endRad = (startDeg + sweepDeg) * Math.PI / 180;

            var outerStart = new Point(cx + outerR * Math.Cos(startRad), cy + outerR * Math.Sin(startRad));
            var outerEnd = new Point(cx + outerR * Math.Cos(endRad), cy + outerR * Math.Sin(endRad));
            var innerStart = new Point(cx + innerR * Math.Cos(startRad), cy + innerR * Math.Sin(startRad));
            var innerEnd = new Point(cx + innerR * Math.Cos(endRad), cy + innerR * Math.Sin(endRad));

            bool largeArc = sweepDeg > 180;

            var figure = new PathFigure { StartPoint = outerStart };
            figure.Segments.Add(new ArcSegment(outerEnd, new Size(outerR, outerR), 0,
                largeArc, SweepDirection.Clockwise, true));
            figure.Segments.Add(new LineSegment(innerEnd, true));
            figure.Segments.Add(new ArcSegment(innerStart, new Size(innerR, innerR), 0,
                largeArc, SweepDirection.Counterclockwise, true));
            figure.IsClosed = true;

            var geom = new PathGeometry();
            geom.Figures.Add(figure);

            return new System.Windows.Shapes.Path
            {
                Data = geom,
                Fill = (Brush)new BrushConverter().ConvertFromString(colorHex),
                Stroke = Brushes.Transparent
            };
        }

        private static string FormatDuration(double totalSeconds)
        {
            var ts = TimeSpan.FromSeconds(totalSeconds);
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{ts.Minutes}m";
        }

        private void SaveTodos()
        {
            try
            {
                var dir = Path.GetDirectoryName(TodosPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(TodosPath, JsonSerializer.Serialize(_todoItems, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void LoadTodos()
        {
            try
            {
                if (!File.Exists(TodosPath)) return;
                var json = File.ReadAllText(TodosPath);
                _todoItems = JsonSerializer.Deserialize<List<TodoItem>>(json) ?? new();
                RefreshTodoList();
            }
            catch { _todoItems = new(); }
        }
        #endregion

        #region Weight Module
        private void SidebarWeight_Click(object sender, RoutedEventArgs e)
        {
            SidebarWeightBorder.Background = (SolidColorBrush)FindResource("mainColorDark");
            SidebarPomodoroBorder.Background = Brushes.Transparent;
            SidebarTodoBorder.Background = Brushes.Transparent;
            SidebarDiaryBorder.Background = Brushes.Transparent;
            ModeTabBar.Visibility = Visibility.Collapsed;
            PomodoroView.Visibility = Visibility.Collapsed;
            TodoView.Visibility = Visibility.Collapsed;
            WeightView.Visibility = Visibility.Visible;
            DiaryView.Visibility = Visibility.Collapsed;

            if (_weightData == null)
            {
                _weightData = WeightData.Load();
                HeightInput.Text = _weightData.HeightCm > 0 ? _weightData.HeightCm.ToString() : "";
                GoalWeightInput.Text = _weightData.GoalWeight > 0 ? _weightData.GoalWeight.ToString("F1") : "";
                WeightDatePicker.SelectedDate = DateTime.Today;
            }
            RefreshWeightChart();
            RefreshWeightStats();
            RefreshWeightHistory();
        }

        private void SidebarDiary_Click(object sender, RoutedEventArgs e)
        {
            SidebarDiaryBorder.Background = (SolidColorBrush)FindResource("mainColorDark");
            SidebarPomodoroBorder.Background = Brushes.Transparent;
            SidebarTodoBorder.Background = Brushes.Transparent;
            SidebarWeightBorder.Background = Brushes.Transparent;
            ModeTabBar.Visibility = Visibility.Collapsed;
            PomodoroView.Visibility = Visibility.Collapsed;
            TodoView.Visibility = Visibility.Collapsed;
            WeightView.Visibility = Visibility.Collapsed;
            DiaryView.Visibility = Visibility.Visible;
        }

        private void RefreshWeightChart()
        {
            if (_weightChart != null)
            {
                WeightChartHost.Child = null;
                _weightChart = null;
            }

            var model = new PlotModel
            {
                Title = null,
                PlotAreaBorderThickness = new OxyPlot.OxyThickness(0),
                Padding = new OxyPlot.OxyThickness(10),
            };

            // Date axis
            var dateAxis = new OxyPlot.Axes.DateTimeAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                StringFormat = "MM/dd",
                IntervalType = OxyPlot.Axes.DateTimeIntervalType.Days,
                MajorStep = 1,
                MinorIntervalType = OxyPlot.Axes.DateTimeIntervalType.Auto,
            };
            model.Axes.Add(dateAxis);

            // Weight axis
            var weightAxis = new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "kg",
                MinimumPadding = 0.1,
                MaximumPadding = 0.1,
            };
            model.Axes.Add(weightAxis);

            if (_weightData.Entries.Count > 0)
            {
                var sorted = _weightData.Entries.OrderBy(e => e.Date).ToList();

                // Weight line
                var lineSeries = new LineSeries
                {
                    Title = "体重",
                    Color = OxyColor.FromRgb(21, 101, 192),
                    StrokeThickness = 2,
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 4,
                    MarkerFill = OxyColor.FromRgb(21, 101, 192),
                    MarkerStroke = OxyColors.White,
                    MarkerStrokeThickness = 1,
                };
                foreach (var entry in sorted)
                {
                    lineSeries.Points.Add(OxyPlot.Axes.DateTimeAxis.CreateDataPoint(entry.Date, entry.Weight));
                }
                model.Series.Add(lineSeries);

                // Goal line
                if (_weightData.GoalWeight > 0)
                {
                    var firstDate = sorted[0].Date;
                    var lastDate = sorted[^1].Date;
                    var goalLine = new LineSeries
                    {
                        Title = $"目标 {_weightData.GoalWeight:F1} kg",
                        Color = OxyColor.FromRgb(46, 125, 50),
                        StrokeThickness = 1.5,
                        LineStyle = LineStyle.Dash,
                    };
                    goalLine.Points.Add(OxyPlot.Axes.DateTimeAxis.CreateDataPoint(firstDate, _weightData.GoalWeight));
                    goalLine.Points.Add(OxyPlot.Axes.DateTimeAxis.CreateDataPoint(lastDate, _weightData.GoalWeight));
                    model.Series.Add(goalLine);
                }

                // Set reasonable axis range
                dateAxis.Minimum = OxyPlot.Axes.DateTimeAxis.ToDouble(sorted[0].Date.AddDays(-1));
                dateAxis.Maximum = OxyPlot.Axes.DateTimeAxis.ToDouble(sorted[^1].Date.AddDays(1));

                // Adaptive X-axis spacing: <10天→1，<30天→3，<60天→7，否则→14
                var daySpan = (sorted[^1].Date - sorted[0].Date).TotalDays;
                dateAxis.MajorStep = daySpan < 10 ? 1 : daySpan < 30 ? 3 : daySpan < 60 ? 7 : 14;
            }

            _weightChart = new PlotView { Model = model };
            WeightChartHost.Child = _weightChart;
        }

        private void RefreshWeightStats()
        {
            if (_weightData.Entries.Count == 0)
            {
                CurrentWeightLabel.Text = "-- kg";
                BmiLabel.Text = "--";
                BmiCategoryLabel.Text = "";
                WeightChangeLabel.Text = "--";
                return;
            }

            var latest = _weightData.Entries.OrderByDescending(e => e.Date).First();
            CurrentWeightLabel.Text = $"{latest.Weight:F1} kg";

            var bmi = _weightData.Bmi;
            BmiLabel.Text = bmi.HasValue ? $"{bmi.Value:F1}" : "--";
            BmiCategoryLabel.Text = _weightData.BmiCategory;

            WeightChangeLabel.Text = _weightData.WeightChangeDisplay;
        }

        private void RefreshWeightHistory()
        {
            WeightHistoryList.ItemsSource = null;
            WeightHistoryList.ItemsSource = _weightData.Entries.OrderByDescending(e => e.Date).ToList();
        }

        private void SaveWeight_Click(object sender, RoutedEventArgs e)
        {
            if (_weightData == null) _weightData = WeightData.Load();

            if (!double.TryParse(WeightInput.Text.Trim(), out var w) || w <= 0 || w > 500)
            {
                MessageBox.Show("请输入有效的体重（kg）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var date = WeightDatePicker.SelectedDate ?? DateTime.Today;

            // Replace existing entry for same date
            var existing = _weightData.Entries.FirstOrDefault(x => x.Date.Date == date.Date);
            if (existing != null)
                existing.Weight = w;
            else
                _weightData.Entries.Add(new WeightEntry { Date = date.Date, Weight = w });

            _weightData.Save();
            WeightInput.Clear();
            RefreshWeightChart();
            RefreshWeightStats();
            RefreshWeightHistory();
        }

        private void DeleteWeight_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DateTime date)
            {
                _weightData.Entries.RemoveAll(x => x.Date.Date == date.Date);
                _weightData.Save();
                RefreshWeightChart();
                RefreshWeightStats();
                RefreshWeightHistory();
            }
        }

        private void WeightInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !double.TryParse(e.Text, out _) && e.Text != ".";
        }

        private void HeightInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_weightData == null) _weightData = WeightData.Load();
            if (double.TryParse(HeightInput.Text.Trim(), out var h) && h > 0 && h < 300)
            {
                _weightData.HeightCm = h;
                _weightData.Save();
                RefreshWeightStats();
            }
        }

        private void GoalWeightInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_weightData == null) _weightData = WeightData.Load();
            if (double.TryParse(GoalWeightInput.Text.Trim(), out var g) && g > 0 && g < 500)
            {
                _weightData.GoalWeight = g;
                _weightData.Save();
                RefreshWeightChart();
            }
        }

        #endregion
    }
}



