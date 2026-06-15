using System;
using System.Media;
using System.Windows;
using System.Windows.Controls;

namespace Pomodoro
{
    public partial class SettingsWindow : Window
    {
        public int PomodoroMinutes { get; private set; }
        public int ShortBreakMinutes { get; private set; }
        public int LongBreakMinutes { get; private set; }
        public int Cycles { get; private set; }
        public int Volume { get; private set; }
        public bool IsMuted { get; private set; }
        public bool IsInfiniteFocus { get; private set; }
        public bool AutoStartNext { get; private set; }
        public double WindowWidth { get; private set; }
        public double WindowHeight { get; private set; }
        public string AmbientSoundKey { get; private set; }
        public string AlarmSoundKey { get; private set; }

        public SettingsWindow(int curPomodoro, int curShortBreak,
                              int curLongBreak, int curCycles, int curVolume, bool curMuted,
                              bool curInfiniteFocus, bool curAutoStart,
                              double curWidth, double curHeight,
                              string curAmbientKey, string curAlarmKey)
        {
            InitializeComponent();

            pomodoroInput.Text = curPomodoro.ToString();
            shortBreakInput.Text = curShortBreak.ToString();
            longBreakInput.Text = curLongBreak.ToString();
            cycleInput.Text = curCycles.ToString();
            VolumeSlider.Value = curVolume;
            VolumeLabel.Text = $"{curVolume}%";
            MuteCheckBox.IsChecked = curMuted;
            InfiniteFocusCheckBox.IsChecked = curInfiniteFocus;
            AutoStartCheckBox.IsChecked = curAutoStart;
            widthInput.Text = curWidth.ToString();
            heightInput.Text = curHeight.ToString();

            AmbientSoundCombo.ItemsSource = SoundGenerator.AmbientSoundNames;
            AlarmSoundCombo.ItemsSource = SoundGenerator.AlarmSoundNames;

            int ambIdx = Array.IndexOf(SoundGenerator.AmbientSoundKeys, curAmbientKey);
            if (ambIdx < 0) ambIdx = 0;
            AmbientSoundCombo.SelectedIndex = ambIdx;

            int almIdx = Array.IndexOf(SoundGenerator.AlarmSoundKeys, curAlarmKey);
            if (almIdx < 0) almIdx = 0;
            AlarmSoundCombo.SelectedIndex = almIdx;
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VolumeLabel != null)
                VolumeLabel.Text = $"{(int)VolumeSlider.Value}%";
        }

        private void MuteCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            VolumeSlider.IsEnabled = !(MuteCheckBox.IsChecked == true);
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(pomodoroInput.Text, out int pom) || pom < 1 || pom > 120)
            {
                MessageBox.Show("专注时长请输入 1-120 之间的整数（分钟）", "输入错误");
                return;
            }
            if (!int.TryParse(shortBreakInput.Text, out int sb) || sb < 1 || sb > 30)
            {
                MessageBox.Show("短休息请输入 1-30 之间的整数（分钟）", "输入错误");
                return;
            }
            if (!int.TryParse(longBreakInput.Text, out int lb) || lb < 1 || lb > 60)
            {
                MessageBox.Show("长休息请输入 1-60 之间的整数（分钟）", "输入错误");
                return;
            }
            if (!int.TryParse(cycleInput.Text, out int cyc) || cyc < 1 || cyc > 20)
            {
                MessageBox.Show("循环次数请输入 1-20 之间的整数", "输入错误");
                return;
            }
            if (!int.TryParse(widthInput.Text, out int w) || w < 300 || w > 2000)
            {
                MessageBox.Show("宽度请输入 300-2000 之间的整数（像素）", "输入错误");
                return;
            }
            if (!int.TryParse(heightInput.Text, out int h) || h < 250 || h > 1500)
            {
                MessageBox.Show("高度请输入 250-1500 之间的整数（像素）", "输入错误");
                return;
            }

            PomodoroMinutes = pom;
            ShortBreakMinutes = sb;
            LongBreakMinutes = lb;
            Cycles = cyc;
            Volume = (int)VolumeSlider.Value;
            IsMuted = MuteCheckBox.IsChecked == true;
            IsInfiniteFocus = InfiniteFocusCheckBox.IsChecked == true;
            AutoStartNext = AutoStartCheckBox.IsChecked == true;
            WindowWidth = w;
            WindowHeight = h;
            AmbientSoundKey = SoundGenerator.AmbientSoundKeys[AmbientSoundCombo.SelectedIndex];
            AlarmSoundKey = SoundGenerator.AlarmSoundKeys[AlarmSoundCombo.SelectedIndex];

            DialogResult = true;
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void PreviewAmbientBtn_Click(object sender, RoutedEventArgs e)
        {
            string key = SoundGenerator.AmbientSoundKeys[AmbientSoundCombo.SelectedIndex];
            byte[] wav = SoundGenerator.GetAmbientSound(key);
            if (wav != null)
            {
                var ms = new System.IO.MemoryStream(wav);
                var player = new SoundPlayer(ms);
                player.Play();
            }
        }

        private void PreviewAlarmBtn_Click(object sender, RoutedEventArgs e)
        {
            string key = SoundGenerator.AlarmSoundKeys[AlarmSoundCombo.SelectedIndex];
            byte[] wav = SoundGenerator.GetAlarmSound(key);
            if (wav != null)
            {
                var ms = new System.IO.MemoryStream(wav);
                var player = new SoundPlayer(ms);
                player.Play();
            }
        }
    }
}
