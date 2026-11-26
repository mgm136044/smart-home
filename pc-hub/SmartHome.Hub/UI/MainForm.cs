using System.Diagnostics;
using SmartHome.Hub.Core;

namespace SmartHome.Hub.UI;

/// <summary>
/// PC 허브 화면: 연결 상태, 장치 제어, 센서값, 온습도 그래프, 통신 로그.
///
/// 화면 꾸미기: 솔루션 탐색기에서 MainForm.cs를 더블클릭하면 디자이너가 열려요.
///   - 배치, 글꼴, 색, 크기는 디자이너에서 마음대로 바꿔도 돼요 (MainForm.Designer.cs가 자동으로 바뀌어요).
///   - 아래 코드가 이름으로 쓰는 컨트롤은 이름(Name)만 바꾸지 말아 주세요:
///     lblLink, chkFake, lblStatus, lblUrls, lblFanState, lblLightState, lblCurtainState, lblDoorState,
///     trkCurtain, lblTemp, lblHum, lblLux, lblIr, formsPlot, lstLog, 그리고 btn으로 시작하는 버튼들.
///   - 켜짐/꺼짐처럼 상태에 따라 바뀌는 색은 Theme.cs에서 바꿔요.
/// </summary>
public partial class MainForm : Form
{
    const int ChartPoints = 180; // 1초 간격 3분

    readonly HubHost _host;
    readonly List<double> _xs = [], _temps = [], _hums = [];

    static MainForm()
    {
        // ScottPlot은 글자를 그릴 때 기본 글꼴 객체를 먼저 써요.
        // 그래프가 만들어지기 전에 바꿔야 한글이 네모로 안 깨져요.
        ScottPlot.Fonts.Default = Theme.ChartFont;
        ScottPlot.Fonts.DefaultFontStyle = SkiaSharp.SKTypeface.FromFamilyName(Theme.ChartFont);
    }

    public MainForm(HubHost host)
    {
        _host = host;
        InitializeComponent();
        SetupChart();

        chkFake.Checked = _host.Fake.Enabled;
        lblUrls.Text = "휴대폰/아이패드 대시보드:  " + string.Join("   ", _host.DashboardUrls());

        foreach (var s in _host.State.History(DateTime.Now.AddSeconds(-ChartPoints))) AddSample(s);
        foreach (var e in _host.Log.Recent()) AddLog(e);

        _host.State.Changed += OnStateChanged;
        _host.State.SensorUpdated += OnSensor;
        _host.Log.Written += OnLog;
    }

    // ───────────── 디자이너가 연결한 이벤트 ─────────────

    void MainForm_Load(object? sender, EventArgs e)
    {
        Render(_host.State.Snapshot());
        RefreshChart();
    }

    void MainForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        _host.State.Changed -= OnStateChanged;
        _host.State.SensorUpdated -= OnSensor;
        _host.Log.Written -= OnLog;
    }

    void chkFake_CheckedChanged(object? sender, EventArgs e) => _host.Fake.Enabled = chkFake.Checked;

    void btnOpenDashboard_Click(object? sender, EventArgs e)
    {
        var url = $"http://localhost:{_host.Settings.WebPort}";
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { lblStatus.Text = $"브라우저 열기 실패: {ex.Message}"; }
    }

    async void btnFanOn_Click(object? sender, EventArgs e) => await RunFrom(sender, Devices.Fan, "on");
    async void btnFanOff_Click(object? sender, EventArgs e) => await RunFrom(sender, Devices.Fan, "off");
    async void btnLightOn_Click(object? sender, EventArgs e) => await RunFrom(sender, Devices.Light, "on");
    async void btnLightOff_Click(object? sender, EventArgs e) => await RunFrom(sender, Devices.Light, "off");
    async void btnCurtainApply_Click(object? sender, EventArgs e) => await RunFrom(sender, Devices.Curtain, "set", trkCurtain.Value);
    async void btnCurtainOpen_Click(object? sender, EventArgs e) => await RunFrom(sender, Devices.Curtain, "open");
    async void btnCurtainClose_Click(object? sender, EventArgs e) => await RunFrom(sender, Devices.Curtain, "close");
    async void btnDoorOpen_Click(object? sender, EventArgs e) => await RunFrom(sender, Devices.Door, "open");
    async void btnDoorClose_Click(object? sender, EventArgs e) => await RunFrom(sender, Devices.Door, "close");

    // ───────────── 동작 ─────────────

    async Task RunFrom(object? sender, string device, string action, int? value = null)
    {
        var button = sender as Button;
        if (button is not null) button.Enabled = false; // 응답이 올 때까지 두 번 누르지 않게
        try
        {
            lblStatus.ForeColor = Theme.Off;
            lblStatus.Text = "보내는 중...";
            var r = await _host.Commands.ExecuteAsync(device, action, value, "winforms");
            lblStatus.Text = r.Ok ? $"완료: {device} → {r.State} ({r.Ms}ms)" : $"실패: {r.Error}";
            lblStatus.ForeColor = r.Ok ? Theme.On : Theme.Bad;
        }
        finally
        {
            if (button is not null) button.Enabled = true;
        }
    }

    void SetupChart()
    {
        var plot = formsPlot.Plot;

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

    // ───────────── 허브 이벤트 (백그라운드 스레드 → UI 스레드) ─────────────

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
        lblLink.Text = s.Connected ? $"● 연결됨: {s.Link}{esp}" : "● 오프라인 (ESP32 연결 없음)";
        lblLink.ForeColor = !s.Connected ? Theme.Bad : s.Esp is { Arduino: false } ? Theme.Warn : Theme.On;

        SetState(lblFanState, s.Fan ? "켜짐" : "꺼짐", s.Fan);
        SetState(lblLightState, s.Light ? "켜짐" : "꺼짐", s.Light);
        SetState(lblCurtainState, $"{s.Curtain}%", s.Curtain > 0);
        SetState(lblDoorState, s.Door ? "열림" : "닫힘", s.Door);
        if (!trkCurtain.Focused) trkCurtain.Value = Math.Clamp(s.Curtain, trkCurtain.Minimum, trkCurtain.Maximum);

        if (chkFake.Checked != _host.Fake.Enabled) chkFake.Checked = _host.Fake.Enabled;
        if (s.Sensors is { } sensors) RenderSensors(sensors);
    }

    static void SetState(Label label, string text, bool on)
    {
        label.Text = text;
        label.ForeColor = on ? Theme.On : Theme.Off;
    }

    void RenderSensors(SensorSample s)
    {
        lblTemp.Text = $"{s.Temp:F1} °C";
        lblHum.Text = $"{s.Hum:F1} %";
        lblLux.Text = $"{s.Lux:F0}";
        lblIr.Text = s.Ir == 1 ? "감지됨" : "없음";
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
        if (_xs.Count > 0) formsPlot.Plot.Axes.AutoScale();
        formsPlot.Refresh();
    }

    void AddLog(LogEntry e)
    {
        lstLog.Items.Insert(0, $"{e.Time:HH:mm:ss}  [{e.Kind}]  {e.Text}");
        while (lstLog.Items.Count > 300) lstLog.Items.RemoveAt(lstLog.Items.Count - 1);
    }
}
