using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QoderCN.Patcher
{
    public class ConfigComboBoxItem
    {
        public string FilePath { get; set; }
        public string DisplayText { get; set; }
        public override string ToString() { return DisplayText; }
    }

    public class MainForm : Form
    {
        private string _appDir;
        private string _configsDir;
        private ProviderProfile _currentProfile;
        private string _currentConfigPath;

        // UI Controls
        private Panel _headerPanel;
        private Label _lblTitle;
        private Label _lblSubTitle;

        private GroupBox _grpStatus;
        private Label _lblStatusIcon;
        private Label _lblStatusText;
        private Label _lblRuntimePath;
        private TextBox _txtInstallDir;
        private Button _btnBrowseInstall;
        private Label _lblQoderProcess;

        private GroupBox _grpConfig;
        private ComboBox _cboConfigs;
        private Button _btnBrowseConfig;
        private Button _btnNewConfig;
        private Button _btnOpenConfigDir;
        private TextBox _txtDisplayName;
        private TextBox _txtUiBaseUrl;
        private TextBox _txtUpstreamUrl;
        private Button _btnTestUrl;
        private DataGridView _gridModels;
        private Button _btnAddModel;
        private Button _btnRemoveModel;
        private Button _btnSaveConfig;

        private GroupBox _grpActions;
        private Button _btnApply;
        private Button _btnRestore;
        private Button _btnDryRun;
        private Button _btnInspect;
        private Button _btnLaunchQoder;

        private GroupBox _grpLog;
        private RichTextBox _rtbLog;
        private Button _btnClearLog;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;

        public MainForm()
        {
            InitializeDirectories();
            InitializeComponent();
            LoadConfigsList();
            RefreshInspection();
        }

        private void InitializeDirectories()
        {
            _appDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = new string[]
            {
                Path.Combine(_appDir, "configs"),
                Path.Combine(Directory.GetParent(_appDir).FullName, "configs"),
                Path.GetFullPath(Path.Combine(_appDir, @"..\..\configs")),
                Path.GetFullPath(Path.Combine(_appDir, @"..\..\..\configs"))
            };

            _configsDir = null;
            foreach (var dir in candidates)
            {
                if (Directory.Exists(dir) && Directory.GetFiles(dir, "*.json").Length > 0)
                {
                    _configsDir = dir;
                    break;
                }
            }
            if (_configsDir == null)
            {
                _configsDir = candidates[0];
            }
        }

        private void InitializeComponent()
        {
            this.Text = "Qoder CN OpenAI 增强修补工具 v2.1 (Direct Custom Routing)";
            this.Size = new Size(1020, 800);
            this.MinimumSize = new Size(960, 740);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            // Header Panel
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = Color.FromArgb(24, 32, 47),
                Padding = new Padding(20, 10, 20, 10)
            };

            _lblTitle = new Label
            {
                Text = "Qoder CN OpenAI-Compatible 增强修补工具",
                Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(18, 12)
            };
            _lblSubTitle = new Label
            {
                Text = "v2.1 原生直连架构 | 本地/中转大模型透明路由 | 零外置代理依赖",
                Font = new Font("Microsoft YaHei UI", 8.5F),
                ForeColor = Color.FromArgb(170, 185, 205),
                AutoSize = true,
                Location = new Point(20, 38)
            };
            _headerPanel.Controls.Add(_lblTitle);
            _headerPanel.Controls.Add(_lblSubTitle);
            this.Controls.Add(_headerPanel);

            int yOffset = 75;

            // 1. Status & Installation Group
            _grpStatus = new GroupBox
            {
                Text = "安装与运行状态",
                Location = new Point(18, yOffset),
                Size = new Size(968, 115),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            };

            var lblPathTag = new Label
            {
                Text = "Qoder 安装目录:",
                Location = new Point(16, 28),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular)
            };
            _txtInstallDir = new TextBox
            {
                Text = PatcherCore.GetDefaultInstallDir(),
                Location = new Point(125, 25),
                Size = new Size(705, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9F)
            };
            _btnBrowseInstall = new Button
            {
                Text = "浏览...",
                Location = new Point(840, 23),
                Size = new Size(110, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            _btnBrowseInstall.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.SelectedPath = _txtInstallDir.Text;
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        _txtInstallDir.Text = dlg.SelectedPath;
                        RefreshInspection();
                    }
                }
            };
            _txtInstallDir.TextChanged += (s, e) => RefreshInspection();

            _lblStatusIcon = new Label
            {
                Text = "●",
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                Location = new Point(16, 60),
                Size = new Size(22, 22),
                ForeColor = Color.Gray
            };
            _lblStatusText = new Label
            {
                Text = "正在检测安装状态...",
                Location = new Point(38, 62),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = Color.DarkSlateGray
            };

            _lblQoderProcess = new Label
            {
                Text = "",
                Location = new Point(420, 62),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(200, 100, 0)
            };

            _lblRuntimePath = new Label
            {
                Text = "",
                Location = new Point(16, 88),
                AutoSize = true,
                Font = new Font("Consolas", 8F),
                ForeColor = Color.Gray
            };

            _grpStatus.Controls.Add(lblPathTag);
            _grpStatus.Controls.Add(_txtInstallDir);
            _grpStatus.Controls.Add(_btnBrowseInstall);
            _grpStatus.Controls.Add(_lblStatusIcon);
            _grpStatus.Controls.Add(_lblStatusText);
            _grpStatus.Controls.Add(_lblQoderProcess);
            _grpStatus.Controls.Add(_lblRuntimePath);
            this.Controls.Add(_grpStatus);

            yOffset += 123;

            // 2. Configuration & Model Management Group
            _grpConfig = new GroupBox
            {
                Text = "上游渠道与模型配置",
                Location = new Point(18, yOffset),
                Size = new Size(968, 255),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            };

            var lblProfileSelect = new Label
            {
                Text = "选择预设配置:",
                Location = new Point(16, 26),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular)
            };
            _cboConfigs = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(125, 23),
                Size = new Size(510, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            _cboConfigs.SelectedIndexChanged += (s, e) => LoadSelectedConfigFile();

            _btnBrowseConfig = new Button
            {
                Text = "导入 JSON...",
                Location = new Point(645, 21),
                Size = new Size(95, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Microsoft YaHei UI", 8.5F)
            };
            _btnBrowseConfig.Click += BtnBrowseConfig_Click;

            _btnNewConfig = new Button
            {
                Text = "+ 新建配置",
                Location = new Point(748, 21),
                Size = new Size(95, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Microsoft YaHei UI", 8.5F)
            };
            _btnNewConfig.Click += BtnNewConfig_Click;

            _btnOpenConfigDir = new Button
            {
                Text = "打开目录",
                Location = new Point(850, 21),
                Size = new Size(100, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Microsoft YaHei UI", 8.5F)
            };
            _btnOpenConfigDir.Click += (s, e) =>
            {
                if (Directory.Exists(_configsDir)) Process.Start("explorer.exe", _configsDir);
            };

            // Provider Edit Fields
            var lblNameTag = new Label
            {
                Text = "渠道显示名称:",
                Location = new Point(16, 58),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular)
            };
            _txtDisplayName = new TextBox
            {
                Location = new Point(125, 55),
                Size = new Size(200, 23),
                Font = new Font("Microsoft YaHei UI", 9F)
            };

            var lblUiUrlTag = new Label
            {
                Text = "界面 UI URL:",
                Location = new Point(335, 58),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular)
            };
            _txtUiBaseUrl = new TextBox
            {
                Location = new Point(415, 55),
                Size = new Size(200, 23),
                Font = new Font("Segoe UI", 9F)
            };

            var lblUpUrlTag = new Label
            {
                Text = "上游 Base URL:",
                Location = new Point(625, 58),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular)
            };
            _txtUpstreamUrl = new TextBox
            {
                Location = new Point(720, 55),
                Size = new Size(135, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9F)
            };
            _btnTestUrl = new Button
            {
                Text = "测试连接",
                Location = new Point(862, 53),
                Size = new Size(88, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Microsoft YaHei UI", 8.5F)
            };
            _btnTestUrl.Click += BtnTestUrl_Click;

            // Models Grid
            _gridModels = new DataGridView
            {
                Location = new Point(16, 88),
                Size = new Size(820, 155),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Font = new Font("Segoe UI", 8.5F),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _gridModels.Columns.Add("id", "模型 ID (model)");
            _gridModels.Columns.Add("displayName", "界面显示别名");
            _gridModels.Columns.Add("maxInputTokens", "最大输入 Tokens");
            _gridModels.Columns["maxInputTokens"].Width = 110;
            _gridModels.Columns.Add(new DataGridViewCheckBoxColumn { Name = "reasoning", HeaderText = "思考(Reasoning)", Width = 90 });
            _gridModels.Columns.Add(new DataGridViewCheckBoxColumn { Name = "vision", HeaderText = "视觉(Vision)", Width = 80 });
            _gridModels.Columns.Add(new DataGridViewCheckBoxColumn { Name = "tools", HeaderText = "工具(Tools)", Width = 80 });

            _btnAddModel = new Button
            {
                Text = "+ 添加模型",
                Location = new Point(845, 88),
                Size = new Size(105, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Microsoft YaHei UI", 8.5F)
            };
            _btnAddModel.Click += BtnAddModel_Click;

            _btnRemoveModel = new Button
            {
                Text = "- 删除所选",
                Location = new Point(845, 122),
                Size = new Size(105, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Microsoft YaHei UI", 8.5F)
            };
            _btnRemoveModel.Click += BtnRemoveModel_Click;

            _btnSaveConfig = new Button
            {
                Text = "💾 保存配置",
                Location = new Point(845, 160),
                Size = new Size(105, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(235, 245, 255),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            };
            _btnSaveConfig.Click += BtnSaveConfig_Click;

            _grpConfig.Controls.Add(lblProfileSelect);
            _grpConfig.Controls.Add(_cboConfigs);
            _grpConfig.Controls.Add(_btnBrowseConfig);
            _grpConfig.Controls.Add(_btnNewConfig);
            _grpConfig.Controls.Add(_btnOpenConfigDir);
            _grpConfig.Controls.Add(lblNameTag);
            _grpConfig.Controls.Add(_txtDisplayName);
            _grpConfig.Controls.Add(lblUiUrlTag);
            _grpConfig.Controls.Add(_txtUiBaseUrl);
            _grpConfig.Controls.Add(lblUpUrlTag);
            _grpConfig.Controls.Add(_txtUpstreamUrl);
            _grpConfig.Controls.Add(_btnTestUrl);
            _grpConfig.Controls.Add(_gridModels);
            _grpConfig.Controls.Add(_btnAddModel);
            _grpConfig.Controls.Add(_btnRemoveModel);
            _grpConfig.Controls.Add(_btnSaveConfig);
            this.Controls.Add(_grpConfig);

            yOffset += 263;

            // 3. Actions Group
            _grpActions = new GroupBox
            {
                Text = "操作执行",
                Location = new Point(18, yOffset),
                Size = new Size(968, 70),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            };

            _btnApply = new Button
            {
                Text = "🚀 一键安装 / 更新修补",
                Location = new Point(16, 22),
                Size = new Size(200, 36),
                BackColor = Color.FromArgb(46, 139, 87),
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            _btnApply.FlatAppearance.BorderSize = 0;
            _btnApply.Click += BtnApply_Click;

            _btnRestore = new Button
            {
                Text = "🔄 恢复官方原版",
                Location = new Point(228, 22),
                Size = new Size(160, 36),
                BackColor = Color.FromArgb(255, 243, 205),
                ForeColor = Color.FromArgb(133, 100, 4),
                Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold)
            };
            _btnRestore.Click += BtnRestore_Click;

            _btnDryRun = new Button
            {
                Text = "🔍 预演检测 (Dry Run)",
                Location = new Point(400, 23),
                Size = new Size(165, 34),
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            _btnDryRun.Click += BtnDryRun_Click;

            _btnInspect = new Button
            {
                Text = "📋 刷新状态",
                Location = new Point(577, 23),
                Size = new Size(125, 34),
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            _btnInspect.Click += (s, e) => RefreshInspection();

            _btnLaunchQoder = new Button
            {
                Text = "⚡ 启动 Qoder CN",
                Location = new Point(734, 23),
                Size = new Size(216, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(240, 240, 240),
                Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold)
            };
            _btnLaunchQoder.Click += BtnLaunchQoder_Click;

            _grpActions.Controls.Add(_btnApply);
            _grpActions.Controls.Add(_btnRestore);
            _grpActions.Controls.Add(_btnDryRun);
            _grpActions.Controls.Add(_btnInspect);
            _grpActions.Controls.Add(_btnLaunchQoder);
            this.Controls.Add(_grpActions);

            yOffset += 78;

            // 4. Log Output Group
            _grpLog = new GroupBox
            {
                Text = "执行日志与诊断输出",
                Location = new Point(18, yOffset),
                Size = new Size(968, 160),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            };

            _rtbLog = new RichTextBox
            {
                Location = new Point(16, 24),
                Size = new Size(936, 122),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 9.5F),
                BorderStyle = BorderStyle.None
            };
            _btnClearLog = new Button
            {
                Text = "清空",
                Location = new Point(885, 0),
                Size = new Size(60, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Microsoft YaHei UI", 8F)
            };
            _btnClearLog.Click += (s, e) => _rtbLog.Clear();

            _grpLog.Controls.Add(_btnClearLog);
            _grpLog.Controls.Add(_rtbLog);
            this.Controls.Add(_grpLog);

            // Status Strip
            _statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel
            {
                Text = "就绪",
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _statusStrip.Items.Add(_statusLabel);
            this.Controls.Add(_statusStrip);

            LogInfo("Qoder CN 增强修补工具 v2.1 初始化完成。");
            if (PatcherCore.IsAdministrator())
            {
                LogSuccess("当前已具备管理员权限运行。");
            }
            else
            {
                LogWarning("当前以普通权限运行。如修补失败请右键选择「以管理员身份运行」。");
            }
        }

        #region Logging Helpers

        private void AppendLog(string message, Color color)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AppendLog(message, color)));
                return;
            }
            _rtbLog.SelectionStart = _rtbLog.TextLength;
            _rtbLog.SelectionLength = 0;
            _rtbLog.SelectionColor = color;
            _rtbLog.AppendText(string.Format("[{0:HH:mm:ss}] {1}\r\n", DateTime.Now, message));
            _rtbLog.SelectionColor = _rtbLog.ForeColor;
            _rtbLog.ScrollToCaret();
        }

        private void LogInfo(string message) { AppendLog(message, Color.FromArgb(100, 180, 255)); }
        private void LogSuccess(string message) { AppendLog(message, Color.FromArgb(120, 230, 120)); }
        private void LogWarning(string message) { AppendLog(message, Color.FromArgb(255, 200, 100)); }
        private void LogError(string message) { AppendLog(message, Color.FromArgb(255, 110, 110)); }

        #endregion

        #region State & Config Logic

        private void RefreshInspection()
        {
            string installDir = _txtInstallDir.Text.Trim();
            var inspection = PatcherCore.Inspect(installDir);

            if (inspection.IsRunning)
            {
                _lblQoderProcess.Text = "⚠️ 检测到 Qoder 正在运行，修补前请先退出！";
                _lblQoderProcess.ForeColor = Color.FromArgb(210, 80, 0);
            }
            else
            {
                _lblQoderProcess.Text = "Qoder 进程未运行 (正常)";
                _lblQoderProcess.ForeColor = Color.DarkGreen;
            }

            switch (inspection.State)
            {
                case PatchState.PatchedV21:
                    _lblStatusIcon.Text = "●";
                    _lblStatusIcon.ForeColor = Color.FromArgb(46, 139, 87);
                    _lblStatusText.Text = "已修补 (v2.1 原生直连 Direct Custom Routing)";
                    _lblStatusText.ForeColor = Color.FromArgb(46, 139, 87);
                    break;
                case PatchState.OriginalSupported:
                    _lblStatusIcon.Text = "●";
                    _lblStatusIcon.ForeColor = Color.FromArgb(0, 122, 204);
                    _lblStatusText.Text = "官方原版 (已就绪，可随时修补)";
                    _lblStatusText.ForeColor = Color.FromArgb(0, 122, 204);
                    break;
                case PatchState.PatchedV20:
                case PatchState.PatchedV1:
                    _lblStatusIcon.Text = "●";
                    _lblStatusIcon.ForeColor = Color.FromArgb(220, 130, 0);
                    _lblStatusText.Text = "已安装早期版本修补 (可直接升级到 v2.1)";
                    _lblStatusText.ForeColor = Color.FromArgb(220, 130, 0);
                    break;
                case PatchState.NotFound:
                    _lblStatusIcon.Text = "●";
                    _lblStatusIcon.ForeColor = Color.Red;
                    _lblStatusText.Text = "未找到 Qoder 运行库文件，请检查目录";
                    _lblStatusText.ForeColor = Color.Red;
                    break;
                default:
                    _lblStatusIcon.Text = "●";
                    _lblStatusIcon.ForeColor = Color.Purple;
                    _lblStatusText.Text = inspection.Message;
                    _lblStatusText.ForeColor = Color.Purple;
                    break;
            }

            _lblRuntimePath.Text = inspection.RuntimePath;
        }

        private void LoadConfigsList(string selectPath = null)
        {
            _cboConfigs.Items.Clear();
            if (Directory.Exists(_configsDir))
            {
                var files = Directory.GetFiles(_configsDir, "*.json");
                Array.Sort(files);
                foreach (var f in files)
                {
                    string label = Path.GetFileName(f);
                    try
                    {
                        string raw = File.ReadAllText(f, Encoding.UTF8);
                        var p = PatcherCore.ParseProfile(raw);
                        if (!string.IsNullOrEmpty(p.displayName))
                        {
                            label = string.Format("{0} ({1})", Path.GetFileName(f), p.displayName);
                        }
                    }
                    catch { }
                    _cboConfigs.Items.Add(new ConfigComboBoxItem { FilePath = f, DisplayText = label });
                }
            }

            if (!string.IsNullOrEmpty(selectPath) && File.Exists(selectPath))
            {
                ConfigComboBoxItem targetItem = null;
                foreach (ConfigComboBoxItem item in _cboConfigs.Items)
                {
                    if (string.Equals(item.FilePath, selectPath, StringComparison.OrdinalIgnoreCase))
                    {
                        targetItem = item;
                        break;
                    }
                }
                if (targetItem == null)
                {
                    targetItem = new ConfigComboBoxItem { FilePath = selectPath, DisplayText = Path.GetFileName(selectPath) };
                    _cboConfigs.Items.Add(targetItem);
                }
                _cboConfigs.SelectedItem = targetItem;
            }
            else if (_cboConfigs.Items.Count > 0)
            {
                _cboConfigs.SelectedIndex = 0;
            }
        }

        private void LoadSelectedConfigFile()
        {
            if (_cboConfigs.SelectedItem == null) return;
            var item = _cboConfigs.SelectedItem as ConfigComboBoxItem;
            string path = item != null ? item.FilePath : _cboConfigs.SelectedItem.ToString();
            _currentConfigPath = path;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                _currentProfile = PatcherCore.ParseProfile(json);

                _txtDisplayName.Text = _currentProfile.displayName ?? "";
                _txtUiBaseUrl.Text = _currentProfile.uiBaseUrl ?? "";
                _txtUpstreamUrl.Text = _currentProfile.upstreamBaseUrl ?? "";

                _gridModels.Rows.Clear();
                if (_currentProfile.models != null)
                {
                    foreach (var m in _currentProfile.models)
                    {
                        _gridModels.Rows.Add(
                            m.id,
                            m.displayName ?? m.id,
                            (m.maxInputTokens.HasValue ? m.maxInputTokens.Value : 131072).ToString(),
                            m.reasoning == true,
                            m.vision == true,
                            m.tools != false
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(string.Format("加载配置文件失败 [{0}]: {1}", Path.GetFileName(path), ex.Message));
            }
        }

        private ProviderProfile BuildProfileFromForm()
        {
            var profile = _currentProfile ?? new ProviderProfile();
            profile.displayName = _txtDisplayName.Text.Trim();
            profile.uiBaseUrl = _txtUiBaseUrl.Text.Trim();
            profile.upstreamBaseUrl = _txtUpstreamUrl.Text.Trim();

            var originalModels = _currentProfile != null && _currentProfile.models != null
                ? _currentProfile.models
                : new List<ModelItem>();

            var newModels = new List<ModelItem>();
            foreach (DataGridViewRow row in _gridModels.Rows)
            {
                if (row.IsNewRow) continue;
                string id = row.Cells["id"].Value != null ? row.Cells["id"].Value.ToString().Trim() : "";
                if (string.IsNullOrEmpty(id)) continue;

                string dname = row.Cells["displayName"].Value != null ? row.Cells["displayName"].Value.ToString().Trim() : "";
                string maxTokensStr = row.Cells["maxInputTokens"].Value != null ? row.Cells["maxInputTokens"].Value.ToString().Trim() : "";
                int maxInputTokens = 131072;
                int parsedTokens;
                if (int.TryParse(maxTokensStr, out parsedTokens) && parsedTokens > 0)
                {
                    maxInputTokens = parsedTokens;
                }

                bool reasoning = Convert.ToBoolean(row.Cells["reasoning"].Value ?? false);
                bool vision = Convert.ToBoolean(row.Cells["vision"].Value ?? false);
                bool tools = Convert.ToBoolean(row.Cells["tools"].Value ?? true);

                ModelItem existing = originalModels.Find(m => m != null && string.Equals(m.id, id, StringComparison.OrdinalIgnoreCase));
                ModelItem item;
                if (existing != null)
                {
                    item = new ModelItem
                    {
                        id = id,
                        displayName = string.IsNullOrEmpty(dname) ? id : dname,
                        maxInputTokens = maxInputTokens,
                        maxOutputTokens = existing.maxOutputTokens,
                        maxTokensField = existing.maxTokensField,
                        efforts = existing.efforts,
                        supportsDisabled = existing.supportsDisabled,
                        reasoning = reasoning,
                        vision = vision,
                        tools = tools
                    };
                }
                else
                {
                    item = new ModelItem
                    {
                        id = id,
                        displayName = string.IsNullOrEmpty(dname) ? id : dname,
                        maxInputTokens = maxInputTokens,
                        reasoning = reasoning,
                        vision = vision,
                        tools = tools
                    };
                }
                newModels.Add(item);
            }

            profile.models = newModels;
            return profile;
        }

        #endregion

        #region UI Event Handlers

        private void BtnBrowseConfig_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "JSON 配置文件 (*.json)|*.json|所有文件 (*.*)|*.*";
                dlg.InitialDirectory = Directory.Exists(_configsDir) ? _configsDir : _appDir;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    LoadConfigsList(dlg.FileName);
                }
            }
        }

        private void BtnNewConfig_Click(object sender, EventArgs e)
        {
            string newPath = Path.Combine(_configsDir, "new-provider-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
            var defaultProfile = new ProviderProfile
            {
                displayName = "自定义 OpenAI 兼容上游",
                uiBaseUrl = "http://127.0.0.1:8000/v1",
                upstreamBaseUrl = "http://127.0.0.1:8000/v1",
                models = new List<ModelItem>
                {
                    new ModelItem { id = "gpt-4o", displayName = "GPT-4o (上游自定义)", maxInputTokens = 131072, tools = true, vision = true },
                    new ModelItem { id = "claude-3-5-sonnet-20241022", displayName = "Claude 3.5 Sonnet", maxInputTokens = 200000, tools = true, vision = true }
                }
            };
            try
            {
                if (!Directory.Exists(_configsDir)) Directory.CreateDirectory(_configsDir);
                File.WriteAllText(newPath, PatcherCore.SerializeProfile(defaultProfile), Encoding.UTF8);
                LoadConfigsList(newPath);
                LogSuccess(string.Format("已创建新配置文件: {0}", Path.GetFileName(newPath)));
            }
            catch (Exception ex)
            {
                LogError("创建配置失败: " + ex.Message);
            }
        }

        private void BtnSaveConfig_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentConfigPath))
            {
                MessageBox.Show("请先选择或新建一个配置文件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var profile = BuildProfileFromForm();
                string json = PatcherCore.SerializeProfile(profile);
                File.WriteAllText(_currentConfigPath, json, Encoding.UTF8);
                _currentProfile = profile;
                LogSuccess(string.Format("配置已成功保存到: {0}", _currentConfigPath));
                MessageBox.Show("配置文件已成功保存！", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadConfigsList(_currentConfigPath);
            }
            catch (Exception ex)
            {
                LogError("保存配置失败: " + ex.Message);
                MessageBox.Show("保存配置失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAddModel_Click(object sender, EventArgs e)
        {
            _gridModels.Rows.Add("custom-model-id", "自定义模型名称", "272000", false, false, true);
        }

        private void BtnRemoveModel_Click(object sender, EventArgs e)
        {
            if (_gridModels.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in _gridModels.SelectedRows)
                {
                    _gridModels.Rows.Remove(row);
                }
            }
        }

        private async void BtnTestUrl_Click(object sender, EventArgs e)
        {
            string url = _txtUpstreamUrl.Text.Trim();
            string normalized = PatcherCore.NormalizeUpstreamUrl(url);
            if (string.IsNullOrEmpty(normalized))
            {
                MessageBox.Show("上游 URL 格式无效，请填写类似 http://127.0.0.1:8000/v1 的地址。", "地址错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LogInfo(string.Format("正在测试上游连通性: {0} ...", normalized));
            _btnTestUrl.Enabled = false;
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(normalized + "/models");
                request.Method = "GET";
                request.Timeout = 5000;
                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                {
                    LogSuccess(string.Format("连通测试成功！HTTP 状态码: {0} ({1})", (int)response.StatusCode, response.StatusDescription));
                }
            }
            catch (WebException wex)
            {
                var resp = wex.Response as HttpWebResponse;
                if (resp != null)
                {
                    LogWarning(string.Format("上游服务已响应 HTTP {0} ({1}) - 服务端存活但可能需要 API Key 或路径不同。", (int)resp.StatusCode, resp.StatusDescription));
                }
                else
                {
                    LogError(string.Format("连通测试失败: {0}", wex.Message));
                }
            }
            catch (Exception ex)
            {
                LogError(string.Format("测试异常: {0}", ex.Message));
            }
            finally
            {
                _btnTestUrl.Enabled = true;
            }
        }

        private void BtnDryRun_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentConfigPath) || !File.Exists(_currentConfigPath))
            {
                MessageBox.Show("请先选择一个有效的配置文件！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                LogInfo("正在执行 Dry Run 预演...");
                string output = PatcherCore.DryRun(_txtInstallDir.Text.Trim(), _currentConfigPath);
                LogSuccess(output);
                _statusLabel.Text = "Dry Run 预演检测通过";
            }
            catch (Exception ex)
            {
                LogError("Dry Run 失败: " + ex.Message);
                _statusLabel.Text = "Dry Run 失败";
            }
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentConfigPath) || !File.Exists(_currentConfigPath))
            {
                MessageBox.Show("请先选择配置文件！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (PatcherCore.IsQoderRunning())
            {
                var dr = MessageBox.Show("检测到 Qoder CN 正在运行中！\r\n\r\n修补会修改核心运行库，请先退出 Qoder CN 再继续。\r\n\r\n是否在退出后重试？", "请先退出 Qoder", MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);
                if (dr != DialogResult.Retry) return;
                if (PatcherCore.IsQoderRunning())
                {
                    LogError("Qoder CN 仍在运行，操作已取消。");
                    return;
                }
            }

            // 保存当前界面的最新配置
            try
            {
                var profile = BuildProfileFromForm();
                File.WriteAllText(_currentConfigPath, PatcherCore.SerializeProfile(profile), Encoding.UTF8);
            }
            catch { }

            try
            {
                LogInfo("正在应用 v2.1 修补并同步运行时配置...");
                PatcherCore.Apply(_txtInstallDir.Text.Trim(), _currentConfigPath);
                LogSuccess("修补安装成功！");
                LogInfo(string.Format("运行时配置已写入: {0}", PatcherCore.GetRuntimeConfigPath()));
                RefreshInspection();
                MessageBox.Show("恭喜！Qoder CN v2.1 增强修补已成功安装！\r\n\r\n您现在可以在 Qoder CN 中直接使用自定义模型了。", "安装成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (UnauthorizedAccessException uex)
            {
                LogError("权限不足: " + uex.Message);
                AskElevateAndExecute("apply");
            }
            catch (Exception ex)
            {
                LogError("修补失败: " + ex.Message);
                MessageBox.Show("修补失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRestore_Click(object sender, EventArgs e)
        {
            if (PatcherCore.IsQoderRunning())
            {
                MessageBox.Show("检测到 Qoder CN 正在运行，请先退出 Qoder CN 再还原。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var dr = MessageBox.Show("确定要将 Qoder CN 还原到最近一次的官方原版备份吗？", "确认还原", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;

            try
            {
                LogInfo("正在从备份中恢复官方原版文件...");
                string backupUsed = PatcherCore.Restore(_txtInstallDir.Text.Trim());
                LogSuccess(string.Format("恢复完成！已应用备份来源: {0}", backupUsed));
                RefreshInspection();
                MessageBox.Show("已成功恢复官方原版！", "恢复成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (UnauthorizedAccessException uex)
            {
                LogError("权限不足: " + uex.Message);
                AskElevateAndExecute("restore");
            }
            catch (Exception ex)
            {
                LogError("恢复失败: " + ex.Message);
                MessageBox.Show("恢复失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AskElevateAndExecute(string action)
        {
            var dr = MessageBox.Show("修补操作需要写入 Program Files，当前缺少管理员权限。\r\n\r\n是否立即以管理员身份重新启动本程序？", "需要管理员提权", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr == DialogResult.Yes)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    Application.Exit();
                }
                catch (Exception ex)
                {
                    LogError("提权启动失败: " + ex.Message);
                }
            }
        }

        private void BtnLaunchQoder_Click(object sender, EventArgs e)
        {
            try
            {
                PatcherCore.LaunchQoder(_txtInstallDir.Text.Trim());
                LogSuccess("已向系统发送 Qoder CN 启动指令。");
            }
            catch (Exception ex)
            {
                LogError("启动 Qoder 失败: " + ex.Message);
                MessageBox.Show("启动失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion
    }
}
