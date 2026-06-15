# 🍅 Pomodoro 个人效率工具

番茄钟 + 时间块 + 待办 + 体重追踪，基于 WPF (.NET 8) 的个人效率软件。

## 📥 下载安装

### 方式一：直接下载（推荐）

1. 安装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)（选 "桌面运行时"）
2. 在 [Releases](https://github.com/AndromedaObserver/pomodoro-wpf/releases) 下载最新 `pomodoro-v1.0.zip`
3. 解压到任意文件夹，双击 `Pomodoro.exe` 运行

### 方式二：从源码构建

```bash
git clone https://github.com/AndromedaObserver/pomodoro-wpf.git
cd pomodoro-wpf/Pomodoro
dotnet run
```

## ✨ 功能

### 🍅 番茄钟
- 专注/短休息/长休息三种模式
- 圆形倒计时盘
- 无限专注模式
- 跳过、自动开始下一轮
- 持续闹铃提醒
- 下一轮预告

### 🎵 环境音
- 7 种 CC0 自然音效：雨声、海浪、溪流、风、篝火、白噪音、滴答声
- 支持循环播放

### 📅 时间块规划
- 24 小时网格可视化
- 12 种预设活动类型
- 点击 + Shift 范围选择 + 拖拽框选
- 批量分配活动
- 配置自动保存

### ✅ 待办列表
- 12 种颜色预设
- 拖拽排序
- JSON 持久化存储

### 📊 体重追踪
- OxyPlot 折线图
- 数据记录与趋势展示

## 🛠 技术栈

- C# / WPF / .NET 8
- OxyPlot（图表）
- NAudio（音频）
- SoundJay CC0 音效素材

## 📄 协议

基于原始项目 [Boutzi/pomodoro](https://github.com/Boutzi/pomodoro) 二次开发，MIT License。
