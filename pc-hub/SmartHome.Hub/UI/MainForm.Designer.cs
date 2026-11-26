#nullable disable

namespace SmartHome.Hub.UI
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tlpRoot = new System.Windows.Forms.TableLayoutPanel();
            this.tlpHeader = new System.Windows.Forms.TableLayoutPanel();
            this.flpHeader = new System.Windows.Forms.FlowLayoutPanel();
            this.lblLink = new System.Windows.Forms.Label();
            this.chkFake = new System.Windows.Forms.CheckBox();
            this.btnOpenDashboard = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblUrls = new System.Windows.Forms.Label();
            this.tlpMiddle = new System.Windows.Forms.TableLayoutPanel();
            this.grpDevices = new System.Windows.Forms.GroupBox();
            this.tlpDevices = new System.Windows.Forms.TableLayoutPanel();
            this.lblFanName = new System.Windows.Forms.Label();
            this.lblFanState = new System.Windows.Forms.Label();
            this.flpFan = new System.Windows.Forms.FlowLayoutPanel();
            this.btnFanOn = new System.Windows.Forms.Button();
            this.btnFanOff = new System.Windows.Forms.Button();
            this.lblLightName = new System.Windows.Forms.Label();
            this.lblLightState = new System.Windows.Forms.Label();
            this.flpLight = new System.Windows.Forms.FlowLayoutPanel();
            this.btnLightOn = new System.Windows.Forms.Button();
            this.btnLightOff = new System.Windows.Forms.Button();
            this.lblCurtainName = new System.Windows.Forms.Label();
            this.lblCurtainState = new System.Windows.Forms.Label();
            this.flpCurtain = new System.Windows.Forms.FlowLayoutPanel();
            this.trkCurtain = new System.Windows.Forms.TrackBar();
            this.btnCurtainApply = new System.Windows.Forms.Button();
            this.btnCurtainOpen = new System.Windows.Forms.Button();
            this.btnCurtainClose = new System.Windows.Forms.Button();
            this.lblDoorName = new System.Windows.Forms.Label();
            this.lblDoorState = new System.Windows.Forms.Label();
            this.flpDoor = new System.Windows.Forms.FlowLayoutPanel();
            this.btnDoorOpen = new System.Windows.Forms.Button();
            this.btnDoorClose = new System.Windows.Forms.Button();
            this.grpSensors = new System.Windows.Forms.GroupBox();
            this.tlpSensors = new System.Windows.Forms.TableLayoutPanel();
            this.lblTempName = new System.Windows.Forms.Label();
            this.lblTemp = new System.Windows.Forms.Label();
            this.lblHumName = new System.Windows.Forms.Label();
            this.lblHum = new System.Windows.Forms.Label();
            this.lblLuxName = new System.Windows.Forms.Label();
            this.lblLux = new System.Windows.Forms.Label();
            this.lblIrName = new System.Windows.Forms.Label();
            this.lblIr = new System.Windows.Forms.Label();
            this.grpChart = new System.Windows.Forms.GroupBox();
            this.formsPlot = new ScottPlot.WinForms.FormsPlot();
            this.grpLog = new System.Windows.Forms.GroupBox();
            this.lstLog = new System.Windows.Forms.ListBox();
            this.tlpRoot.SuspendLayout();
            this.tlpHeader.SuspendLayout();
            this.flpHeader.SuspendLayout();
            this.tlpMiddle.SuspendLayout();
            this.grpDevices.SuspendLayout();
            this.tlpDevices.SuspendLayout();
            this.flpFan.SuspendLayout();
            this.flpLight.SuspendLayout();
            this.flpCurtain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trkCurtain)).BeginInit();
            this.flpDoor.SuspendLayout();
            this.grpSensors.SuspendLayout();
            this.tlpSensors.SuspendLayout();
            this.grpChart.SuspendLayout();
            this.grpLog.SuspendLayout();
            this.SuspendLayout();
            //
            // tlpRoot
            //
            this.tlpRoot.ColumnCount = 1;
            this.tlpRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRoot.Controls.Add(this.tlpHeader, 0, 0);
            this.tlpRoot.Controls.Add(this.tlpMiddle, 0, 1);
            this.tlpRoot.Controls.Add(this.grpChart, 0, 2);
            this.tlpRoot.Controls.Add(this.grpLog, 0, 3);
            this.tlpRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpRoot.Location = new System.Drawing.Point(0, 0);
            this.tlpRoot.Name = "tlpRoot";
            this.tlpRoot.Padding = new System.Windows.Forms.Padding(12);
            this.tlpRoot.RowCount = 4;
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 58F));
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 42F));
            this.tlpRoot.Size = new System.Drawing.Size(1000, 780);
            this.tlpRoot.TabIndex = 0;
            //
            // tlpHeader
            //
            this.tlpHeader.AutoSize = true;
            this.tlpHeader.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpHeader.ColumnCount = 1;
            this.tlpHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpHeader.Controls.Add(this.flpHeader, 0, 0);
            this.tlpHeader.Controls.Add(this.lblUrls, 0, 1);
            this.tlpHeader.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpHeader.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
            this.tlpHeader.Name = "tlpHeader";
            this.tlpHeader.RowCount = 2;
            this.tlpHeader.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpHeader.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpHeader.TabIndex = 0;
            //
            // flpHeader
            //
            this.flpHeader.AutoSize = true;
            this.flpHeader.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flpHeader.Controls.Add(this.lblLink);
            this.flpHeader.Controls.Add(this.chkFake);
            this.flpHeader.Controls.Add(this.btnOpenDashboard);
            this.flpHeader.Controls.Add(this.lblStatus);
            this.flpHeader.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpHeader.Margin = new System.Windows.Forms.Padding(0);
            this.flpHeader.Name = "flpHeader";
            this.flpHeader.TabIndex = 0;
            //
            // lblLink
            //
            this.lblLink.AutoSize = true;
            this.lblLink.Font = new System.Drawing.Font("Malgun Gothic", 11F, System.Drawing.FontStyle.Bold);
            this.lblLink.ForeColor = System.Drawing.Color.DimGray;
            this.lblLink.Margin = new System.Windows.Forms.Padding(3, 7, 16, 3);
            this.lblLink.Name = "lblLink";
            this.lblLink.TabIndex = 0;
            this.lblLink.Text = "● 연결 확인 중";
            //
            // chkFake
            //
            this.chkFake.AutoSize = true;
            this.chkFake.Margin = new System.Windows.Forms.Padding(3, 7, 12, 3);
            this.chkFake.Name = "chkFake";
            this.chkFake.TabIndex = 1;
            this.chkFake.Text = "가상 장치 사용";
            this.chkFake.UseVisualStyleBackColor = true;
            this.chkFake.CheckedChanged += new System.EventHandler(this.chkFake_CheckedChanged);
            //
            // btnOpenDashboard
            //
            this.btnOpenDashboard.AutoSize = true;
            this.btnOpenDashboard.Name = "btnOpenDashboard";
            this.btnOpenDashboard.TabIndex = 2;
            this.btnOpenDashboard.Text = "대시보드 열기";
            this.btnOpenDashboard.UseVisualStyleBackColor = true;
            this.btnOpenDashboard.Click += new System.EventHandler(this.btnOpenDashboard_Click);
            //
            // lblStatus
            //
            this.lblStatus.AutoSize = true;
            this.lblStatus.ForeColor = System.Drawing.Color.DimGray;
            this.lblStatus.Margin = new System.Windows.Forms.Padding(12, 9, 3, 3);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.TabIndex = 3;
            //
            // lblUrls
            //
            this.lblUrls.AutoSize = true;
            this.lblUrls.ForeColor = System.Drawing.Color.DimGray;
            this.lblUrls.Margin = new System.Windows.Forms.Padding(3, 2, 3, 6);
            this.lblUrls.Name = "lblUrls";
            this.lblUrls.TabIndex = 1;
            this.lblUrls.Text = "휴대폰/아이패드 대시보드:";
            //
            // tlpMiddle
            //
            this.tlpMiddle.AutoSize = true;
            this.tlpMiddle.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpMiddle.ColumnCount = 2;
            this.tlpMiddle.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 68F));
            this.tlpMiddle.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 32F));
            this.tlpMiddle.Controls.Add(this.grpDevices, 0, 0);
            this.tlpMiddle.Controls.Add(this.grpSensors, 1, 0);
            this.tlpMiddle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpMiddle.Margin = new System.Windows.Forms.Padding(0);
            this.tlpMiddle.Name = "tlpMiddle";
            this.tlpMiddle.RowCount = 1;
            this.tlpMiddle.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpMiddle.TabIndex = 1;
            //
            // grpDevices
            //
            this.grpDevices.AutoSize = true;
            this.grpDevices.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.grpDevices.Controls.Add(this.tlpDevices);
            this.grpDevices.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpDevices.Name = "grpDevices";
            this.grpDevices.Padding = new System.Windows.Forms.Padding(8);
            this.grpDevices.TabIndex = 0;
            this.grpDevices.TabStop = false;
            this.grpDevices.Text = "장치 제어";
            //
            // tlpDevices
            //
            this.tlpDevices.AutoSize = true;
            this.tlpDevices.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpDevices.ColumnCount = 3;
            this.tlpDevices.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpDevices.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpDevices.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpDevices.Controls.Add(this.lblFanName, 0, 0);
            this.tlpDevices.Controls.Add(this.lblFanState, 1, 0);
            this.tlpDevices.Controls.Add(this.flpFan, 2, 0);
            this.tlpDevices.Controls.Add(this.lblLightName, 0, 1);
            this.tlpDevices.Controls.Add(this.lblLightState, 1, 1);
            this.tlpDevices.Controls.Add(this.flpLight, 2, 1);
            this.tlpDevices.Controls.Add(this.lblCurtainName, 0, 2);
            this.tlpDevices.Controls.Add(this.lblCurtainState, 1, 2);
            this.tlpDevices.Controls.Add(this.flpCurtain, 2, 2);
            this.tlpDevices.Controls.Add(this.lblDoorName, 0, 3);
            this.tlpDevices.Controls.Add(this.lblDoorState, 1, 3);
            this.tlpDevices.Controls.Add(this.flpDoor, 2, 3);
            this.tlpDevices.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpDevices.Name = "tlpDevices";
            this.tlpDevices.RowCount = 4;
            this.tlpDevices.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpDevices.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpDevices.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpDevices.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpDevices.TabIndex = 0;
            //
            // lblFanName
            //
            this.lblFanName.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblFanName.AutoSize = true;
            this.lblFanName.ForeColor = System.Drawing.Color.DimGray;
            this.lblFanName.Margin = new System.Windows.Forms.Padding(3, 6, 12, 6);
            this.lblFanName.Name = "lblFanName";
            this.lblFanName.Text = "팬";
            //
            // lblFanState
            //
            this.lblFanState.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblFanState.Font = new System.Drawing.Font("Malgun Gothic", 10F, System.Drawing.FontStyle.Bold);
            this.lblFanState.ForeColor = System.Drawing.Color.DimGray;
            this.lblFanState.Name = "lblFanState";
            this.lblFanState.Size = new System.Drawing.Size(72, 28);
            this.lblFanState.Text = "-";
            this.lblFanState.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // flpFan
            //
            this.flpFan.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.flpFan.AutoSize = true;
            this.flpFan.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flpFan.Controls.Add(this.btnFanOn);
            this.flpFan.Controls.Add(this.btnFanOff);
            this.flpFan.Margin = new System.Windows.Forms.Padding(0);
            this.flpFan.Name = "flpFan";
            this.flpFan.WrapContents = false;
            //
            // btnFanOn
            //
            this.btnFanOn.AutoSize = true;
            this.btnFanOn.MinimumSize = new System.Drawing.Size(64, 30);
            this.btnFanOn.Name = "btnFanOn";
            this.btnFanOn.TabIndex = 0;
            this.btnFanOn.Text = "켜기";
            this.btnFanOn.UseVisualStyleBackColor = true;
            this.btnFanOn.Click += new System.EventHandler(this.btnFanOn_Click);
            //
            // btnFanOff
            //
            this.btnFanOff.AutoSize = true;
            this.btnFanOff.MinimumSize = new System.Drawing.Size(64, 30);
            this.btnFanOff.Name = "btnFanOff";
            this.btnFanOff.TabIndex = 1;
            this.btnFanOff.Text = "끄기";
            this.btnFanOff.UseVisualStyleBackColor = true;
            this.btnFanOff.Click += new System.EventHandler(this.btnFanOff_Click);
            //
            // lblLightName
            //
            this.lblLightName.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblLightName.AutoSize = true;
            this.lblLightName.ForeColor = System.Drawing.Color.DimGray;
            this.lblLightName.Margin = new System.Windows.Forms.Padding(3, 6, 12, 6);
            this.lblLightName.Name = "lblLightName";
            this.lblLightName.Text = "전등";
            //
            // lblLightState
            //
            this.lblLightState.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblLightState.Font = new System.Drawing.Font("Malgun Gothic", 10F, System.Drawing.FontStyle.Bold);
            this.lblLightState.ForeColor = System.Drawing.Color.DimGray;
            this.lblLightState.Name = "lblLightState";
            this.lblLightState.Size = new System.Drawing.Size(72, 28);
            this.lblLightState.Text = "-";
            this.lblLightState.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // flpLight
            //
            this.flpLight.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.flpLight.AutoSize = true;
            this.flpLight.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flpLight.Controls.Add(this.btnLightOn);
            this.flpLight.Controls.Add(this.btnLightOff);
            this.flpLight.Margin = new System.Windows.Forms.Padding(0);
            this.flpLight.Name = "flpLight";
            this.flpLight.WrapContents = false;
            //
            // btnLightOn
            //
            this.btnLightOn.AutoSize = true;
            this.btnLightOn.MinimumSize = new System.Drawing.Size(64, 30);
            this.btnLightOn.Name = "btnLightOn";
            this.btnLightOn.TabIndex = 0;
            this.btnLightOn.Text = "켜기";
            this.btnLightOn.UseVisualStyleBackColor = true;
            this.btnLightOn.Click += new System.EventHandler(this.btnLightOn_Click);
            //
            // btnLightOff
            //
            this.btnLightOff.AutoSize = true;
            this.btnLightOff.MinimumSize = new System.Drawing.Size(64, 30);
            this.btnLightOff.Name = "btnLightOff";
            this.btnLightOff.TabIndex = 1;
            this.btnLightOff.Text = "끄기";
            this.btnLightOff.UseVisualStyleBackColor = true;
            this.btnLightOff.Click += new System.EventHandler(this.btnLightOff_Click);
            //
            // lblCurtainName
            //
            this.lblCurtainName.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblCurtainName.AutoSize = true;
            this.lblCurtainName.ForeColor = System.Drawing.Color.DimGray;
            this.lblCurtainName.Margin = new System.Windows.Forms.Padding(3, 6, 12, 6);
            this.lblCurtainName.Name = "lblCurtainName";
            this.lblCurtainName.Text = "커튼";
            //
            // lblCurtainState
            //
            this.lblCurtainState.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblCurtainState.Font = new System.Drawing.Font("Malgun Gothic", 10F, System.Drawing.FontStyle.Bold);
            this.lblCurtainState.ForeColor = System.Drawing.Color.DimGray;
            this.lblCurtainState.Name = "lblCurtainState";
            this.lblCurtainState.Size = new System.Drawing.Size(72, 28);
            this.lblCurtainState.Text = "-";
            this.lblCurtainState.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // flpCurtain
            //
            this.flpCurtain.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.flpCurtain.AutoSize = true;
            this.flpCurtain.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flpCurtain.Controls.Add(this.trkCurtain);
            this.flpCurtain.Controls.Add(this.btnCurtainApply);
            this.flpCurtain.Controls.Add(this.btnCurtainOpen);
            this.flpCurtain.Controls.Add(this.btnCurtainClose);
            this.flpCurtain.Margin = new System.Windows.Forms.Padding(0);
            this.flpCurtain.Name = "flpCurtain";
            this.flpCurtain.WrapContents = false;
            //
            // trkCurtain
            //
            this.trkCurtain.AutoSize = false;
            this.trkCurtain.LargeChange = 10;
            this.trkCurtain.Maximum = 100;
            this.trkCurtain.Name = "trkCurtain";
            this.trkCurtain.Size = new System.Drawing.Size(160, 32);
            this.trkCurtain.SmallChange = 10;
            this.trkCurtain.TabIndex = 0;
            this.trkCurtain.TickFrequency = 10;
            //
            // btnCurtainApply
            //
            this.btnCurtainApply.AutoSize = true;
            this.btnCurtainApply.MinimumSize = new System.Drawing.Size(64, 30);
            this.btnCurtainApply.Name = "btnCurtainApply";
            this.btnCurtainApply.TabIndex = 1;
            this.btnCurtainApply.Text = "적용";
            this.btnCurtainApply.UseVisualStyleBackColor = true;
            this.btnCurtainApply.Click += new System.EventHandler(this.btnCurtainApply_Click);
            //
            // btnCurtainOpen
            //
            this.btnCurtainOpen.AutoSize = true;
            this.btnCurtainOpen.MinimumSize = new System.Drawing.Size(64, 30);
            this.btnCurtainOpen.Name = "btnCurtainOpen";
            this.btnCurtainOpen.TabIndex = 2;
            this.btnCurtainOpen.Text = "열기";
            this.btnCurtainOpen.UseVisualStyleBackColor = true;
            this.btnCurtainOpen.Click += new System.EventHandler(this.btnCurtainOpen_Click);
            //
            // btnCurtainClose
            //
            this.btnCurtainClose.AutoSize = true;
            this.btnCurtainClose.MinimumSize = new System.Drawing.Size(64, 30);
            this.btnCurtainClose.Name = "btnCurtainClose";
            this.btnCurtainClose.TabIndex = 3;
            this.btnCurtainClose.Text = "닫기";
            this.btnCurtainClose.UseVisualStyleBackColor = true;
            this.btnCurtainClose.Click += new System.EventHandler(this.btnCurtainClose_Click);
            //
            // lblDoorName
            //
            this.lblDoorName.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblDoorName.AutoSize = true;
            this.lblDoorName.ForeColor = System.Drawing.Color.DimGray;
            this.lblDoorName.Margin = new System.Windows.Forms.Padding(3, 6, 12, 6);
            this.lblDoorName.Name = "lblDoorName";
            this.lblDoorName.Text = "문";
            //
            // lblDoorState
            //
            this.lblDoorState.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblDoorState.Font = new System.Drawing.Font("Malgun Gothic", 10F, System.Drawing.FontStyle.Bold);
            this.lblDoorState.ForeColor = System.Drawing.Color.DimGray;
            this.lblDoorState.Name = "lblDoorState";
            this.lblDoorState.Size = new System.Drawing.Size(72, 28);
            this.lblDoorState.Text = "-";
            this.lblDoorState.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // flpDoor
            //
            this.flpDoor.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.flpDoor.AutoSize = true;
            this.flpDoor.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flpDoor.Controls.Add(this.btnDoorOpen);
            this.flpDoor.Controls.Add(this.btnDoorClose);
            this.flpDoor.Margin = new System.Windows.Forms.Padding(0);
            this.flpDoor.Name = "flpDoor";
            this.flpDoor.WrapContents = false;
            //
            // btnDoorOpen
            //
            this.btnDoorOpen.AutoSize = true;
            this.btnDoorOpen.MinimumSize = new System.Drawing.Size(64, 30);
            this.btnDoorOpen.Name = "btnDoorOpen";
            this.btnDoorOpen.TabIndex = 0;
            this.btnDoorOpen.Text = "열기";
            this.btnDoorOpen.UseVisualStyleBackColor = true;
            this.btnDoorOpen.Click += new System.EventHandler(this.btnDoorOpen_Click);
            //
            // btnDoorClose
            //
            this.btnDoorClose.AutoSize = true;
            this.btnDoorClose.MinimumSize = new System.Drawing.Size(64, 30);
            this.btnDoorClose.Name = "btnDoorClose";
            this.btnDoorClose.TabIndex = 1;
            this.btnDoorClose.Text = "닫기";
            this.btnDoorClose.UseVisualStyleBackColor = true;
            this.btnDoorClose.Click += new System.EventHandler(this.btnDoorClose_Click);
            //
            // grpSensors
            //
            this.grpSensors.AutoSize = true;
            this.grpSensors.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.grpSensors.Controls.Add(this.tlpSensors);
            this.grpSensors.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpSensors.Name = "grpSensors";
            this.grpSensors.Padding = new System.Windows.Forms.Padding(8);
            this.grpSensors.TabIndex = 1;
            this.grpSensors.TabStop = false;
            this.grpSensors.Text = "센서";
            //
            // tlpSensors
            //
            this.tlpSensors.AutoSize = true;
            this.tlpSensors.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpSensors.ColumnCount = 2;
            this.tlpSensors.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpSensors.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpSensors.Controls.Add(this.lblTempName, 0, 0);
            this.tlpSensors.Controls.Add(this.lblTemp, 1, 0);
            this.tlpSensors.Controls.Add(this.lblHumName, 0, 1);
            this.tlpSensors.Controls.Add(this.lblHum, 1, 1);
            this.tlpSensors.Controls.Add(this.lblLuxName, 0, 2);
            this.tlpSensors.Controls.Add(this.lblLux, 1, 2);
            this.tlpSensors.Controls.Add(this.lblIrName, 0, 3);
            this.tlpSensors.Controls.Add(this.lblIr, 1, 3);
            this.tlpSensors.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpSensors.Name = "tlpSensors";
            this.tlpSensors.RowCount = 4;
            this.tlpSensors.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpSensors.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpSensors.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpSensors.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpSensors.TabIndex = 0;
            //
            // lblTempName
            //
            this.lblTempName.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblTempName.AutoSize = true;
            this.lblTempName.ForeColor = System.Drawing.Color.DimGray;
            this.lblTempName.Margin = new System.Windows.Forms.Padding(3, 6, 12, 6);
            this.lblTempName.Name = "lblTempName";
            this.lblTempName.Text = "온도";
            //
            // lblTemp
            //
            this.lblTemp.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblTemp.AutoSize = true;
            this.lblTemp.Font = new System.Drawing.Font("Malgun Gothic", 12F, System.Drawing.FontStyle.Bold);
            this.lblTemp.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.lblTemp.Name = "lblTemp";
            this.lblTemp.Text = "-";
            //
            // lblHumName
            //
            this.lblHumName.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblHumName.AutoSize = true;
            this.lblHumName.ForeColor = System.Drawing.Color.DimGray;
            this.lblHumName.Margin = new System.Windows.Forms.Padding(3, 6, 12, 6);
            this.lblHumName.Name = "lblHumName";
            this.lblHumName.Text = "습도";
            //
            // lblHum
            //
            this.lblHum.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblHum.AutoSize = true;
            this.lblHum.Font = new System.Drawing.Font("Malgun Gothic", 12F, System.Drawing.FontStyle.Bold);
            this.lblHum.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.lblHum.Name = "lblHum";
            this.lblHum.Text = "-";
            //
            // lblLuxName
            //
            this.lblLuxName.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblLuxName.AutoSize = true;
            this.lblLuxName.ForeColor = System.Drawing.Color.DimGray;
            this.lblLuxName.Margin = new System.Windows.Forms.Padding(3, 6, 12, 6);
            this.lblLuxName.Name = "lblLuxName";
            this.lblLuxName.Text = "조도";
            //
            // lblLux
            //
            this.lblLux.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblLux.AutoSize = true;
            this.lblLux.Font = new System.Drawing.Font("Malgun Gothic", 12F, System.Drawing.FontStyle.Bold);
            this.lblLux.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.lblLux.Name = "lblLux";
            this.lblLux.Text = "-";
            //
            // lblIrName
            //
            this.lblIrName.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblIrName.AutoSize = true;
            this.lblIrName.ForeColor = System.Drawing.Color.DimGray;
            this.lblIrName.Margin = new System.Windows.Forms.Padding(3, 6, 12, 6);
            this.lblIrName.Name = "lblIrName";
            this.lblIrName.Text = "사람 감지";
            //
            // lblIr
            //
            this.lblIr.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblIr.AutoSize = true;
            this.lblIr.Font = new System.Drawing.Font("Malgun Gothic", 12F, System.Drawing.FontStyle.Bold);
            this.lblIr.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.lblIr.Name = "lblIr";
            this.lblIr.Text = "-";
            //
            // grpChart
            //
            this.grpChart.Controls.Add(this.formsPlot);
            this.grpChart.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpChart.Name = "grpChart";
            this.grpChart.Padding = new System.Windows.Forms.Padding(6);
            this.grpChart.TabIndex = 2;
            this.grpChart.TabStop = false;
            this.grpChart.Text = "온습도 (최근 3분)";
            //
            // formsPlot
            //
            this.formsPlot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.formsPlot.Name = "formsPlot";
            this.formsPlot.TabIndex = 0;
            //
            // grpLog
            //
            this.grpLog.Controls.Add(this.lstLog);
            this.grpLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpLog.Name = "grpLog";
            this.grpLog.Padding = new System.Windows.Forms.Padding(6);
            this.grpLog.TabIndex = 3;
            this.grpLog.TabStop = false;
            this.grpLog.Text = "통신 로그 (최신이 위)";
            //
            // lstLog
            //
            this.lstLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstLog.Font = new System.Drawing.Font("Consolas", 9F);
            this.lstLog.HorizontalScrollbar = true;
            this.lstLog.IntegralHeight = false;
            this.lstLog.Name = "lstLog";
            this.lstLog.TabIndex = 0;
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(1000, 780);
            this.Controls.Add(this.tlpRoot);
            this.Font = new System.Drawing.Font("Malgun Gothic", 9.5F);
            this.MinimumSize = new System.Drawing.Size(860, 620);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "SmartHome Hub";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.MainForm_FormClosed);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.tlpRoot.ResumeLayout(false);
            this.tlpRoot.PerformLayout();
            this.tlpHeader.ResumeLayout(false);
            this.tlpHeader.PerformLayout();
            this.flpHeader.ResumeLayout(false);
            this.flpHeader.PerformLayout();
            this.tlpMiddle.ResumeLayout(false);
            this.tlpMiddle.PerformLayout();
            this.grpDevices.ResumeLayout(false);
            this.grpDevices.PerformLayout();
            this.tlpDevices.ResumeLayout(false);
            this.tlpDevices.PerformLayout();
            this.flpFan.ResumeLayout(false);
            this.flpFan.PerformLayout();
            this.flpLight.ResumeLayout(false);
            this.flpLight.PerformLayout();
            this.flpCurtain.ResumeLayout(false);
            this.flpCurtain.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trkCurtain)).EndInit();
            this.flpDoor.ResumeLayout(false);
            this.flpDoor.PerformLayout();
            this.grpSensors.ResumeLayout(false);
            this.grpSensors.PerformLayout();
            this.tlpSensors.ResumeLayout(false);
            this.tlpSensors.PerformLayout();
            this.grpChart.ResumeLayout(false);
            this.grpLog.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tlpRoot;
        private System.Windows.Forms.TableLayoutPanel tlpHeader;
        private System.Windows.Forms.FlowLayoutPanel flpHeader;
        private System.Windows.Forms.Label lblLink;
        private System.Windows.Forms.CheckBox chkFake;
        private System.Windows.Forms.Button btnOpenDashboard;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label lblUrls;
        private System.Windows.Forms.TableLayoutPanel tlpMiddle;
        private System.Windows.Forms.GroupBox grpDevices;
        private System.Windows.Forms.TableLayoutPanel tlpDevices;
        private System.Windows.Forms.Label lblFanName;
        private System.Windows.Forms.Label lblFanState;
        private System.Windows.Forms.FlowLayoutPanel flpFan;
        private System.Windows.Forms.Button btnFanOn;
        private System.Windows.Forms.Button btnFanOff;
        private System.Windows.Forms.Label lblLightName;
        private System.Windows.Forms.Label lblLightState;
        private System.Windows.Forms.FlowLayoutPanel flpLight;
        private System.Windows.Forms.Button btnLightOn;
        private System.Windows.Forms.Button btnLightOff;
        private System.Windows.Forms.Label lblCurtainName;
        private System.Windows.Forms.Label lblCurtainState;
        private System.Windows.Forms.FlowLayoutPanel flpCurtain;
        private System.Windows.Forms.TrackBar trkCurtain;
        private System.Windows.Forms.Button btnCurtainApply;
        private System.Windows.Forms.Button btnCurtainOpen;
        private System.Windows.Forms.Button btnCurtainClose;
        private System.Windows.Forms.Label lblDoorName;
        private System.Windows.Forms.Label lblDoorState;
        private System.Windows.Forms.FlowLayoutPanel flpDoor;
        private System.Windows.Forms.Button btnDoorOpen;
        private System.Windows.Forms.Button btnDoorClose;
        private System.Windows.Forms.GroupBox grpSensors;
        private System.Windows.Forms.TableLayoutPanel tlpSensors;
        private System.Windows.Forms.Label lblTempName;
        private System.Windows.Forms.Label lblTemp;
        private System.Windows.Forms.Label lblHumName;
        private System.Windows.Forms.Label lblHum;
        private System.Windows.Forms.Label lblLuxName;
        private System.Windows.Forms.Label lblLux;
        private System.Windows.Forms.Label lblIrName;
        private System.Windows.Forms.Label lblIr;
        private System.Windows.Forms.GroupBox grpChart;
        private ScottPlot.WinForms.FormsPlot formsPlot;
        private System.Windows.Forms.GroupBox grpLog;
        private System.Windows.Forms.ListBox lstLog;
    }
}
