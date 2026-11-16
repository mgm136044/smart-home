using System.Diagnostics;
using SmartHome.Hub.Core;

namespace SmartHome.Hub.UI;

/// <summary>PC 허브 화면: 연결 상태, 장치 제어, 센서값, 온습도 그래프, 통신 로그</summary>
public sealed class MainForm : Form
{
    const int ChartPoints = 180; // 1초 간격 3분

    readonly HubHost _host;

    readonly Label _link = new() { AutoSize = true, Font = new Font("Malgun Gothic", 11f, FontStyle.Bold), Margin = new Padding(3, 7, 16, 3) };
    readonly CheckBox _fake = new() { Text = "가상 장치 사용", AutoSize = true, Margin = new Padding(3, 7, 12, 3) };
    readonly Label _status = new() { AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(12, 9, 3, 3) };
    readonly Label _urls = new() { AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(3, 2, 3, 6) };

    readonly Label _fanState = StateLabel();
    readonly Label _lightState = StateLabel();
    readonly Label _curtainState = StateLabel();
    readonly Label _doorState = StateLabel();
    readonly TrackBar _curtain = new() { Minimum = 0, Maximum = 100, TickFrequency = 10, SmallChange = 10, LargeChange = 10, Width = 160, AutoSize = false, Height = 32 };

    readonly Label _temp = SensorLabel();
    readonly Label _hum = SensorLabel();
    readonly Label _lux = SensorLabel();
    readonly Label _ir = SensorLabel();

    readonly ScottPlot.WinForms.FormsPlot _plot = new() { Dock = DockStyle.Fill };
    readonly List<double> _xs = [], _temps = [], _hums = [];

    readonly ListBox _logList = new()
    {
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 9f),
        IntegralHeight = false,
        HorizontalScrollbar = true,
    };

    static MainForm()
    {
        // ScottPlot은 글자를 그릴 때 FontName보다 Font(글꼴 객체)를 먼저 써요.
        // 그래프(FormsPlot)가 만들어지기 전에 기본 글꼴을 맑은 고딕으로 바꿔야 한글이 네모로 안 깨져요.
        ScottPlot.Fonts.Default = "Malgun Gothic";
        ScottPlot.Fonts.DefaultFontStyle = SkiaSharp.SKTypeface.FromFamilyName("Malgun Gothic");
    }

    public MainForm(HubHost host)
    {
        _host = host;
        Text = "SmartHome Hub";
        Font = new Font("Malgun Gothic", 9.5f);
        ClientSize = new Size(1000, 780);
        MinimumSize = new Size(860, 620);
        StartPosition = FormStartPosition.CenterScreen;

        BuildLayout();
        SetupChart();

        _fake.Checked = _host.Fake.Enabled;
        _fake.CheckedChanged += (_, _) => _host.Fake.Enabled = _fake.Checked;
        _urls.Text = "휴대폰/아이패드 대시보드:  " + string.Join("   ", _host.DashboardUrls());

        foreach (var s in _host.State.History(DateTime.Now.AddSeconds(-ChartPoints))) AddSample(s);
        foreach (var e in _host.Log.Recent()) AddLog(e);

        Load += (_, _) =>
        {
            Render(_host.State.Snapshot());
            RefreshChart();
        };

        _host.State.Changed += OnStateChanged;
        _host.State.SensorUpdated += OnSensor;
        _host.Log.Written += OnLog;
        FormClosed += (_, _) =>
        {
            _host.State.Changed -= OnStateChanged;
            _host.State.SensorUpdated -= OnSensor;
            _host.Log.Written -= OnLog;
        };
    }

    // ───────────── 화면 구성 ─────────────

    void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

        // 1) 상단: 연결 상태
        var openDashboard = new Button { Text = "대시보드 열기", AutoSize = true };
        openDashboard.Click += (_, _) => OpenDashboard();
        var headerRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = Padding.Empty };
        headerRow.Controls.AddRange([_link, _fake, openDashboard, _status]);
        var header = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, Margin = new Padding(0, 0, 0, 6) };
        header.Controls.Add(headerRow);
        header.Controls.Add(_urls);

        // 2) 장치 제어 + 센서
        var devices = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill };
        AddRow(devices, "팬", _fanState, Btn("켜기", () => Run(Devices.Fan, "on")), Btn("끄기", () => Run(Devices.Fan, "off")));
        AddRow(devices, "전등", _lightState, Btn("켜기", () => Run(Devices.Light, "on")), Btn("끄기", () => Run(Devices.Light, "off")));
        AddRow(devices, "커튼", _curtainState, _curtain,
            Btn("적용", () => Run(Devices.Curtain, "set", _curtain.Value)),
            Btn("열기", () => Run(Devices.Curtain, "open")),
            Btn("닫기", () => Run(Devices.Curtain, "close")));
        AddRow(devices, "문", _doorState, Btn("열기", () => Run(Devices.Door, "open")), Btn("닫기", () => Run(Devices.Door, "close")));
        var deviceGroup = new GroupBox { Text = "장치 제어", AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(8) };
        deviceGroup.Controls.Add(devices);

        var sensors = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill };
        AddRow(sensors, "온도", _temp);
        AddRow(sensors, "습도", _hum);
        AddRow(sensors, "조도", _lux);
        AddRow(sensors, "사람 감지", _ir);
        var sensorGroup = new GroupBox { Text = "센서", AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(8) };
        sensorGroup.Controls.Add(sensors);

        var middle = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        middle.Controls.Add(deviceGroup, 0, 0);
        middle.Controls.Add(sensorGroup, 1, 0);

        // 3) 그래프, 4) 로그
        var chartGroup = new GroupBox { Text = "온습도 (최근 3분)", Dock = DockStyle.Fill, Padding = new Padding(6) };
        chartGroup.Controls.Add(_plot);
        var logGroup = new GroupBox { Text = "통신 로그 (최신이 위)", Dock = DockStyle.Fill, Padding = new Padding(6) };
        logGroup.Controls.Add(_logList);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(middle, 0, 1);
        root.Controls.Add(chartGroup, 0, 2);
        root.Controls.Add(logGroup, 0, 3);
        Controls.Add(root);
    }

    static void AddRow(TableLayoutPanel table, string name, Label value, params Control[] actions)
    {
        var row = table.RowCount;
        table.RowCount = row + 1;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = name,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = Color.DimGray,
            Margin = new Padding(3, 6, 12, 6),
        };
        value.Anchor = AnchorStyles.Left;
        table.Controls.Add(label, 0, row);
        table.Controls.Add(value, 1, row);

        if (actions.Length > 0)
        {
            var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty, Anchor = AnchorStyles.Left };
            flow.Controls.AddRange(actions);
            table.Controls.Add(flow, 2, row);
        }
    }

    Button Btn(string text, Func<Task> action)
    {
        var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(64, 30) };
        button.Click += async (_, _) =>
        {
            button.Enabled = false;
            try { await action(); }
            finally { button.Enabled = true; }
        };
        return button;
    }

    static Label StateLabel() => new()
    {
        AutoSize = false,
        Size = new Size(72, 28),
        TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font("Malgun Gothic", 10f, FontStyle.Bold),
        ForeColor = Color.DimGray,
        Text = "-",
    };

    static Label SensorLabel() => new()
    {
        AutoSize = true,
        Font = new Font("Malgun Gothic", 12f, FontStyle.Bold),
        Text = "-",
        Margin = new Padding(3, 4, 3, 4),
    };

    void SetupChart()
    {
        var plot = _plot.Plot;

        var temp = plot.Add.Scatter(_xs, _temps);
        temp.LegendText = "온도 (°C)";
        temp.MarkerSize = 0;
        temp.LineWidth = 2;

        var hum = plot.Add.Scatter(_xs, _hums);
        hum.LegendText = "습도 (%)";
        hum.MarkerSize = 0;
        hum.LineWidth = 2;
        hum.Axes.YAxis = plot.Axes.Right; // 습도는 오른쪽 축

        var timeAxis = plot.Axes.DateTimeTicksBottom();
        if (timeAxis.TickGenerator is ScottPlot.TickGenerators.DateTimeAutomatic ticks)
            ticks.LabelFormatter = t => t.ToString("HH:mm:ss");

        plot.Axes.Left.Label.Text = "온도 (°C)";
        plot.Axes.Right.Label.Text = "습도 (%)";
        plot.ShowLegend(ScottPlot.Alignment.UpperLeft);
    }

    // ───────────── 동작 ─────────────

    async Task Run(string device, string action, int? value = null)
    {
        _status.ForeColor = Color.DimGray;
        _status.Text = "보내는 중...";
        var r = await _host.Commands.ExecuteAsync(device, action, value, "winforms");
        _status.Text = r.Ok ? $"완료: {device} → {r.State} ({r.Ms}ms)" : $"실패: {r.Error}";
        _status.ForeColor = r.Ok ? Color.SeaGreen : Color.Firebrick;
    }

    void OpenDashboard()
    {
        var url = $"http://localhost:{_host.Settings.WebPort}";
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { _status.Text = $"브라우저 열기 실패: {ex.Message}"; }
    }

    // ───────────── 이벤트 (백그라운드 스레드 → UI 스레드) ─────────────

    void OnStateChanged(HomeSnapshot s) => Ui(() => Render(s));

    void OnSensor(SensorSample s) => Ui(() =>
    {
        AddSample(s);
        RenderSensors(s);
        RefreshChart();
    });

    void OnLog(LogEntry e) => Ui(() => AddLog(e));

    void Ui(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); }
        catch (InvalidOperationException) { } // 창이 닫히는 중 (ObjectDisposedException 포함)
    }

    void Render(HomeSnapshot s)
    {
        var esp = s.Esp is { } e
            ? (e.Rssi is { } rssi ? $" · 신호 {rssi}dBm" : "") + (e.Simulated ? " · ESP32 단독 모드" : "") + (e.Arduino ? "" : " · Arduino 응답 없음")
            : "";
        _link.Text = s.Connected ? $"● 연결됨: {s.Link}{esp}" : "● 오프라인 (ESP32 연결 없음)";
        _link.ForeColor = !s.Connected ? Color.Firebrick : s.Esp is { Arduino: false } ? Color.DarkOrange : Color.SeaGreen;

        SetState(_fanState, s.Fan ? "켜짐" : "꺼짐", s.Fan);
        SetState(_lightState, s.Light ? "켜짐" : "꺼짐", s.Light);
        SetState(_curtainState, $"{s.Curtain}%", s.Curtain > 0);
        SetState(_doorState, s.Door ? "열림" : "닫힘", s.Door);
        if (!_curtain.Focused) _curtain.Value = Math.Clamp(s.Curtain, 0, 100);

        if (_fake.Checked != _host.Fake.Enabled) _fake.Checked = _host.Fake.Enabled;
        if (s.Sensors is { } sensors) RenderSensors(sensors);
    }

    static void SetState(Label label, string text, bool on)
    {
        label.Text = text;
        label.ForeColor = on ? Color.SeaGreen : Color.DimGray;
    }

    void RenderSensors(SensorSample s)
    {
        _temp.Text = $"{s.Temp:F1} °C";
        _hum.Text = $"{s.Hum:F1} %";
        _lux.Text = $"{s.Lux:F0}";
        _ir.Text = s.Ir == 1 ? "감지됨" : "없음";
    }

    void AddSample(SensorSample s)
    {
        _xs.Add(s.Time.ToOADate());
        _temps.Add(s.Temp);
        _hums.Add(s.Hum);
        while (_xs.Count > ChartPoints)
        {
            _xs.RemoveAt(0);
            _temps.RemoveAt(0);
            _hums.RemoveAt(0);
        }
    }

    void RefreshChart()
    {
        if (_xs.Count > 0) _plot.Plot.Axes.AutoScale();
        _plot.Refresh();
    }

    void AddLog(LogEntry e)
    {
        _logList.Items.Insert(0, $"{e.Time:HH:mm:ss}  [{e.Kind}]  {e.Text}");
        while (_logList.Items.Count > 300) _logList.Items.RemoveAt(_logList.Items.Count - 1);
    }
}
