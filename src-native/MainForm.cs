using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QoderCN.GatewayManager
{
    public enum GuiState
    {
        Checking,
        InvalidDirectory,
        IncompatibleRuntime,
        IncompleteProfile,
        ApiKeyMissing,
        RunningConflict,
        ReadyToPatchBaseline,
        ReadyToUpgradeOlder,
        ConfigOutOfSync,
        ReadyToLaunch,
        RunningHealthy
    }

    public class MainForm : Form
    {
        private string projectRoot;
        private string configsDir;
        private string workspaceFilePath;
        private string runtimeConfigPath;
        private string backupRoot;

        private GatewayWorkspace workspace;

        private Label titleLbl;
        private Label subtitleLbl;
        private Label statusBadgeLbl;

        // Top Header & Qoder Directory Bar
        private Panel headerPanel;
        private Panel pathPanel;
        private TextBox installText;
        private Button browseInstallButton;

        // Main Tab Control
        private TabControl tabControl;
        private TabPage tabProviders;
        private TabPage tabModels;
        private TabPage tabLogs;

        // Tab 1: Providers Controls
        private ListView providerListView;
        private Button addProviderBtn;
        private Button editProviderBtn;
        private Button removeProviderBtn;
        private Button fetchModelsBtn;
        private Button testProviderBtn;
        private Button importProfileBtn;

        // Tab 2: Model Pool Controls
        private TextBox modelSearchText;
        private ComboBox providerFilterCombo;
        private CheckedListBox modelCheckList;
        private Button selectAllButton;
        private Button uncheckAllButton;
        private Button addModelButton;
        private Button editModelButton;
        private Button removeModelButton;
        private Label modelCountLbl;

        private List<ModelItem> filteredModelList = new List<ModelItem>();
        private HashSet<string> checkedModelKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool isApplyingModelFilter = false;

        // Bottom Action Bar & Tools
        private Panel bottomBar;
        private Button primaryActionButton;
        private Button applyConfigButton;
        private Button launchButton;
        private Button restoreButton;
        private Button inspectButton;
        private Button refreshButton;

        // Tab 3: Activity Log & Diagnostics
        private RichTextBox outputBox;
        private Button copyLogButton;
        private Button clearLogButton;
        private ToolTip toolTip;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;

        private Timer pollTimer;
        private GuiState currentState = GuiState.Checking;
        private bool isBusy = false;

        public MainForm()
        {
            InitializePaths();
            InitializeWorkspace();
            InitializeComponents();
            InitializeTimer();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshProviderList();
            RefreshProviderFilterCombo();
            ApplyModelFilter();
            ReevaluateState(true);
        }

        private void InitializePaths()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            if (Directory.Exists(Path.Combine(exeDir, "configs")))
            {
                projectRoot = exeDir;
            }
            else if (Directory.Exists(Path.Combine(Path.GetDirectoryName(exeDir.TrimEnd('\\')), "configs")))
            {
                projectRoot = Path.GetDirectoryName(exeDir.TrimEnd('\\'));
            }
            else
            {
                projectRoot = exeDir;
            }

            configsDir = Path.Combine(projectRoot, "configs");
            if (!Directory.Exists(configsDir)) Directory.CreateDirectory(configsDir);

            workspaceFilePath = Path.Combine(configsDir, "gateway-workspace.json");
            string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            runtimeConfigPath = Path.Combine(userHome, @".qoder-cn\custom-openai-provider-v3.2.0.json");
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            backupRoot = Path.Combine(localAppData, @"QoderCNOpenAICompatiblePatcher\backups-v2");
        }

        private void InitializeWorkspace()
        {
            if (File.Exists(workspaceFilePath))
            {
                workspace = GatewayWorkspace.LoadFromFile(workspaceFilePath);
            }
            else
            {
                // Look for existing legacy profile in configs/ directory to migrate seamlessly
                string legacyProfile = Path.Combine(configsDir, "cpa-192.168.50.241.json");
                if (!File.Exists(legacyProfile))
                {
                    string[] jsonFiles = Directory.GetFiles(configsDir, "*.json");
                    if (jsonFiles.Length > 0) legacyProfile = jsonFiles[0];
                }

                if (File.Exists(legacyProfile))
                {
                    try
                    {
                        ProviderConfig oldCfg = ProviderConfig.LoadFromFile(legacyProfile);
                        workspace = GatewayWorkspace.FromSingleProfile(oldCfg, Path.GetFileNameWithoutExtension(legacyProfile));
                        workspace.SaveToFile(workspaceFilePath);
                    }
                    catch
                    {
                        workspace = new GatewayWorkspace();
                    }
                }
                else
                {
                    workspace = new GatewayWorkspace();
                    ProviderItem defaultProvider = new ProviderItem
                    {
                        id = "p-cpa",
                        name = "CPA @ 192.168.50.241",
                        baseUrl = "http://192.168.50.241:8317/v1",
                        uiBaseUrl = "https://192.168.50.241:8317/v1",
                        firstPayloadTimeoutMs = 180000,
                        streamIdleTimeoutMs = 300000,
                        enabled = true
                    };
                    defaultProvider.models.Add(new ModelItem
                    {
                        id = "gpt-5.6-terra",
                        displayName = "GPT-5.6 Terra",
                        vision = false,
                        reasoning = true,
                        tools = true,
                        maxInputTokens = 131072,
                        maxOutputTokens = 32768,
                        providerId = defaultProvider.id,
                        providerName = defaultProvider.name,
                        upstreamBaseUrl = defaultProvider.baseUrl,
                        uiBaseUrl = defaultProvider.GetEffectiveUiUrl(),
                        selectedForInjection = true
                    });
                    workspace.providers.Add(defaultProvider);
                    workspace.selectedModelKeys.Add(defaultProvider.models[0].id);
                    workspace.SaveToFile(workspaceFilePath);
                }
            }

            checkedModelKeys.Clear();
            foreach (ProviderItem p in workspace.providers)
            {
                if (p.models == null) continue;
                foreach (ModelItem m in p.models)
                {
                    string uniqueKey = string.Format("{0}::{1}", p.id, m.id);
                    if (m.selectedForInjection || workspace.selectedModelKeys.Contains(m.id) || workspace.selectedModelKeys.Contains(uniqueKey))
                    {
                        checkedModelKeys.Add(uniqueKey);
                        checkedModelKeys.Add(m.id);
                    }
                }
            }
        }

        private void InitializeTimer()
        {
            pollTimer = new Timer();
            pollTimer.Interval = 2500;
            pollTimer.Tick += (s, e) =>
            {
                if (!isBusy && IsHandleCreated)
                {
                    ReevaluateState(false);
                }
            };
            pollTimer.Start();
        }

        private void InitializeComponents()
        {
            Text = "Qoder CN AI Gateway Manager v3.2.0-beta";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1100, 780);
            MinimumSize = new Size(980, 640);
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;

            toolTip = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 400,
                ReshowDelay = 150,
                ShowAlways = true
            };

            try
            {
                string iconPath = Path.Combine(projectRoot, @"src-native\app.ico");
                if (File.Exists(iconPath)) Icon = new Icon(iconPath);
            }
            catch { }

            // 1. Status Strip (Dock Bottom)
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel { Text = "Ready", Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            statusStrip.Items.Add(statusLabel);
            Controls.Add(statusStrip);

            // 2. Bottom Action Bar (Dock Bottom)
            InitializeBottomBar();

            // 3. Header Panel (Dock Top)
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 54,
                BackColor = Color.White,
                Padding = new Padding(16, 8, 16, 6)
            };
            Controls.Add(headerPanel);

            titleLbl = new Label
            {
                Text = "Qoder CN AI Gateway Manager",
                Font = new Font("Segoe UI Semibold", 13.5f),
                AutoSize = true,
                Location = new Point(14, 6)
            };
            headerPanel.Controls.Add(titleLbl);

            subtitleLbl = new Label
            {
                Text = "Multi-Provider Gateway & Unified Model Pool Selector (v3.2.0)",
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = true,
                Location = new Point(16, 30)
            };
            headerPanel.Controls.Add(subtitleLbl);

            statusBadgeLbl = new Label
            {
                Text = "🟡 检查中...",
                Font = new Font("Segoe UI Semibold", 9.5f),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                ForeColor = Color.DarkSlateBlue
            };
            statusBadgeLbl.Location = new Point(headerPanel.Width - 160, 14);
            headerPanel.Controls.Add(statusBadgeLbl);
            headerPanel.Resize += (s, e) =>
            {
                statusBadgeLbl.Location = new Point(headerPanel.Width - statusBadgeLbl.Width - 20, 14);
            };

            // 4. Qoder Path Banner (Dock Top)
            pathPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding = new Padding(16, 8, 16, 8)
            };
            Controls.Add(pathPanel);

            TableLayoutPanel pathGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            pathGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pathGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            pathPanel.Controls.Add(pathGrid);

            Label lblInstall = new Label
            {
                Text = "Qoder CN 安装目录:",
                Font = new Font("Segoe UI Semibold", 9f),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 8, 0)
            };
            pathGrid.Controls.Add(lblInstall, 0, 0);

            installText = new TextBox
            {
                Text = @"C:\Program Files\Qoder\Qoder CN",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 8, 0)
            };
            installText.TextChanged += (s, e) => ReevaluateState();
            pathGrid.Controls.Add(installText, 1, 0);

            browseInstallButton = new Button
            {
                Text = "📁 浏览...",
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            browseInstallButton.Click += BrowseInstallButton_Click;
            pathGrid.Controls.Add(browseInstallButton, 2, 0);

            // 5. Main Tab Control (Dock Fill)
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(14, 6)
            };
            Controls.Add(tabControl);
            tabControl.BringToFront();

            tabProviders = new TabPage { Text = " 🌐 AI Providers (提供方管理) " };
            tabModels = new TabPage { Text = " 🎯 Model Pool (模型池与注入) " };
            tabLogs = new TabPage { Text = " 📜 Logs & Diagnostics (活动日志与诊断) " };

            tabControl.TabPages.Add(tabProviders);
            tabControl.TabPages.Add(tabModels);
            tabControl.TabPages.Add(tabLogs);

            InitializeProvidersTab();
            InitializeModelsTab();
            InitializeLogsTab();
        }

        private void InitializeProvidersTab()
        {
            TableLayoutPanel provLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 2,
                Padding = new Padding(10),
                Margin = new Padding(0)
            };
            provLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            provLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 195F));
            tabProviders.Controls.Add(provLayout);

            providerListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                Margin = new Padding(0, 0, 10, 0)
            };
            providerListView.Columns.Add("Provider 名称", 190);
            providerListView.Columns.Add("Base URL (v1 Endpoint)", 280);
            providerListView.Columns.Add("模型数", 75);
            providerListView.Columns.Add("API Key 凭据状态", 175);
            providerListView.Columns.Add("超时设置", 120);
            providerListView.SelectedIndexChanged += (s, e) => UpdateProviderActionButtons();
            providerListView.DoubleClick += (s, e) => EditProviderBtn_Click(s, e);
            provLayout.Controls.Add(providerListView, 0, 0);

            FlowLayoutPanel actionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0)
            };
            provLayout.Controls.Add(actionsPanel, 1, 0);

            int btnWidth = 185;
            int btnHeight = 34;
            Padding btnMargin = new Padding(0, 0, 0, 8);

            addProviderBtn = new Button
            {
                Text = "➕ 添加 Provider",
                Size = new Size(btnWidth, btnHeight),
                BackColor = Color.FromArgb(235, 245, 255),
                Margin = btnMargin
            };
            addProviderBtn.Click += AddProviderBtn_Click;
            actionsPanel.Controls.Add(addProviderBtn);

            editProviderBtn = new Button
            {
                Text = "✏️ 编辑 Provider",
                Size = new Size(btnWidth, btnHeight),
                Enabled = false,
                Margin = btnMargin
            };
            editProviderBtn.Click += EditProviderBtn_Click;
            actionsPanel.Controls.Add(editProviderBtn);

            removeProviderBtn = new Button
            {
                Text = "🗑️ 删除 Provider",
                Size = new Size(btnWidth, btnHeight),
                Enabled = false,
                Margin = btnMargin
            };
            removeProviderBtn.Click += RemoveProviderBtn_Click;
            actionsPanel.Controls.Add(removeProviderBtn);

            fetchModelsBtn = new Button
            {
                Text = "⚡ 拉取模型 (/models)",
                Size = new Size(btnWidth, btnHeight),
                BackColor = Color.FromArgb(240, 250, 242),
                Enabled = false,
                Margin = btnMargin
            };
            fetchModelsBtn.Click += async (s, e) => await FetchModelsForSelectedProvider();
            toolTip.SetToolTip(fetchModelsBtn, "Query GET {baseUrl}/models to discover models under this provider");
            actionsPanel.Controls.Add(fetchModelsBtn);

            testProviderBtn = new Button
            {
                Text = "🔍 测试连通性",
                Size = new Size(btnWidth, btnHeight),
                Enabled = false,
                Margin = btnMargin
            };
            testProviderBtn.Click += async (s, e) => await TestSelectedProviderConn();
            actionsPanel.Controls.Add(testProviderBtn);

            importProfileBtn = new Button
            {
                Text = "📂 导入旧版 JSON",
                Size = new Size(btnWidth, 30),
                Font = new Font("Segoe UI", 8.5f),
                Margin = new Padding(0, 16, 0, 0)
            };
            importProfileBtn.Click += ImportProfileBtn_Click;
            actionsPanel.Controls.Add(importProfileBtn);
        }

        private void InitializeModelsTab()
        {
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 2,
                Padding = new Padding(10),
                Margin = new Padding(0)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 195F));
            tabModels.Controls.Add(mainLayout);

            // Filter row
            FlowLayoutPanel filterPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 6)
            };
            mainLayout.Controls.Add(filterPanel, 0, 0);
            mainLayout.SetColumnSpan(filterPanel, 2);

            Label lblSearch = new Label { Text = "🔍 搜索:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) };
            filterPanel.Controls.Add(lblSearch);

            modelSearchText = new TextBox
            {
                Size = new Size(200, 24),
                Margin = new Padding(0, 3, 16, 0)
            };
            modelSearchText.TextChanged += (s, e) => ApplyModelFilter();
            filterPanel.Controls.Add(modelSearchText);

            Label lblProv = new Label { Text = "所属 Provider:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) };
            filterPanel.Controls.Add(lblProv);

            providerFilterCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Size = new Size(180, 24),
                Margin = new Padding(0, 3, 20, 0)
            };
            providerFilterCombo.SelectedIndexChanged += (s, e) => ApplyModelFilter();
            filterPanel.Controls.Add(providerFilterCombo);

            modelCountLbl = new Label
            {
                Text = "已勾选 0 / 0 个模型准备注入 Qoder",
                AutoSize = true,
                ForeColor = Color.Navy,
                Font = new Font("Segoe UI Semibold", 9f),
                Margin = new Padding(0, 6, 0, 0)
            };
            filterPanel.Controls.Add(modelCountLbl);

            modelCheckList = new CheckedListBox
            {
                CheckOnClick = true,
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                Margin = new Padding(0, 0, 10, 0)
            };
            modelCheckList.ItemCheck += (s, e) =>
            {
                if (isApplyingModelFilter) return;
                if (e.Index >= 0 && e.Index < modelCheckList.Items.Count)
                {
                    ModelItem item = (ModelItem)modelCheckList.Items[e.Index];
                    string uniqueKey = string.Format("{0}::{1}", item.providerId, item.id);
                    item.selectedForInjection = (e.NewValue == CheckState.Checked);
                    if (e.NewValue == CheckState.Checked)
                    {
                        checkedModelKeys.Add(uniqueKey);
                        checkedModelKeys.Add(item.id);
                    }
                    else
                    {
                        checkedModelKeys.Remove(uniqueKey);
                        checkedModelKeys.Remove(item.id);
                    }
                }
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(() => { UpdateModelCountLabel(); ReevaluateState(); }));
                }
                else
                {
                    UpdateModelCountLabel();
                    ReevaluateState();
                }
            };
            modelCheckList.DoubleClick += (s, e) => EditModelButton_Click(s, e);
            mainLayout.Controls.Add(modelCheckList, 0, 1);

            // Model Action buttons
            FlowLayoutPanel actionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0)
            };
            mainLayout.Controls.Add(actionsPanel, 1, 1);

            int btnWidth = 185;
            int btnHeight = 34;
            Padding btnMargin = new Padding(0, 0, 0, 8);

            selectAllButton = new Button
            {
                Text = "全选当前 (All)",
                Size = new Size(btnWidth, btnHeight),
                Margin = btnMargin
            };
            selectAllButton.Click += (s, e) =>
            {
                for (int i = 0; i < modelCheckList.Items.Count; i++)
                {
                    modelCheckList.SetItemChecked(i, true);
                    ModelItem m = (ModelItem)modelCheckList.Items[i];
                    string uniqueKey = string.Format("{0}::{1}", m.providerId, m.id);
                    m.selectedForInjection = true;
                    checkedModelKeys.Add(uniqueKey);
                    checkedModelKeys.Add(m.id);
                }
                UpdateModelCountLabel();
                ReevaluateState();
            };
            actionsPanel.Controls.Add(selectAllButton);

            uncheckAllButton = new Button
            {
                Text = "取消全选 (None)",
                Size = new Size(btnWidth, btnHeight),
                Margin = btnMargin
            };
            uncheckAllButton.Click += (s, e) =>
            {
                for (int i = 0; i < modelCheckList.Items.Count; i++)
                {
                    modelCheckList.SetItemChecked(i, false);
                    ModelItem m = (ModelItem)modelCheckList.Items[i];
                    string uniqueKey = string.Format("{0}::{1}", m.providerId, m.id);
                    m.selectedForInjection = false;
                    checkedModelKeys.Remove(uniqueKey);
                    checkedModelKeys.Remove(m.id);
                }
                UpdateModelCountLabel();
                ReevaluateState();
            };
            actionsPanel.Controls.Add(uncheckAllButton);

            addModelButton = new Button
            {
                Text = "➕ 手动添加模型...",
                Size = new Size(btnWidth, btnHeight),
                BackColor = Color.FromArgb(235, 245, 255),
                Margin = btnMargin
            };
            addModelButton.Click += AddModelButton_Click;
            actionsPanel.Controls.Add(addModelButton);

            editModelButton = new Button
            {
                Text = "✏️ 编辑模型参数...",
                Size = new Size(btnWidth, btnHeight),
                Margin = btnMargin
            };
            editModelButton.Click += EditModelButton_Click;
            actionsPanel.Controls.Add(editModelButton);

            removeModelButton = new Button
            {
                Text = "🗑️ 移除模型",
                Size = new Size(btnWidth, btnHeight),
                Margin = btnMargin
            };
            removeModelButton.Click += RemoveModelButton_Click;
            actionsPanel.Controls.Add(removeModelButton);
        }

        private void InitializeLogsTab()
        {
            TableLayoutPanel logLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(10),
                Margin = new Padding(0)
            };
            logLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            logLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tabLogs.Controls.Add(logLayout);

            FlowLayoutPanel topBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = new Padding(0)
            };
            logLayout.Controls.Add(topBar, 0, 0);

            Label outLbl = new Label
            {
                Text = "活动日志与诊断控制台 (Activity & Diagnostics Console):",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9f),
                Margin = new Padding(0, 6, 20, 0)
            };
            topBar.Controls.Add(outLbl);

            copyLogButton = new Button
            {
                Text = "📋 复制日志",
                Size = new Size(95, 26),
                Margin = new Padding(0, 2, 8, 0)
            };
            copyLogButton.Click += CopyLogButton_Click;
            toolTip.SetToolTip(copyLogButton, "Copy entire log output to Windows Clipboard");
            topBar.Controls.Add(copyLogButton);

            clearLogButton = new Button
            {
                Text = "🧹 清屏",
                Size = new Size(75, 26),
                Margin = new Padding(0, 2, 0, 0)
            };
            clearLogButton.Click += (s, e) => { outputBox.Clear(); };
            toolTip.SetToolTip(clearLogButton, "Clear log console");
            topBar.Controls.Add(clearLogButton);

            outputBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(250, 250, 250),
                Font = new Font("Consolas", 9f),
                WordWrap = false,
                Margin = new Padding(0)
            };
            logLayout.Controls.Add(outputBox, 0, 1);
        }

        private void InitializeBottomBar()
        {
            bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(12, 9, 12, 9)
            };
            Controls.Add(bottomBar);

            // Left Utilities (Dock Left)
            FlowLayoutPanel leftFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0)
            };
            bottomBar.Controls.Add(leftFlow);

            // Right Primary Actions (Dock Fill to take all remaining width)
            FlowLayoutPanel rightFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0)
            };
            bottomBar.Controls.Add(rightFlow);
            rightFlow.BringToFront();

            refreshButton = new Button
            {
                Text = "🔄 刷新",
                Size = new Size(85, 34),
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, 0, 6, 0)
            };
            refreshButton.Click += (s, e) =>
            {
                RefreshProviderList();
                RefreshProviderFilterCombo();
                ApplyModelFilter();
                ReevaluateState(true);
            };
            toolTip.SetToolTip(refreshButton, "Reload workspace & re-evaluate state (F5)");
            leftFlow.Controls.Add(refreshButton);

            inspectButton = new Button
            {
                Text = "🔍 诊断",
                Size = new Size(85, 34),
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, 0, 6, 0)
            };
            inspectButton.Click += (s, e) =>
            {
                tabControl.SelectedTab = tabLogs;
                PerformInspect();
            };
            toolTip.SetToolTip(inspectButton, "Inspect Qoder installation and checksum integrity");
            leftFlow.Controls.Add(inspectButton);

            restoreButton = new Button
            {
                Text = "🛡️ 还原备份",
                Size = new Size(130, 34),
                Font = new Font("Segoe UI", 9f),
                BackColor = Color.FromArgb(255, 245, 235),
                Margin = new Padding(0, 0, 6, 0)
            };
            restoreButton.Click += RestoreButton_Click;
            toolTip.SetToolTip(restoreButton, "Restore original runtime from verified backup (Requires UAC)");
            leftFlow.Controls.Add(restoreButton);

            launchButton = new Button
            {
                Text = "🚀 启动 Qoder CN",
                Size = new Size(160, 34),
                Font = new Font("Segoe UI Semibold", 9f),
                Margin = new Padding(6, 0, 0, 0)
            };
            launchButton.Click += (s, e) => LaunchQoder();
            toolTip.SetToolTip(launchButton, "Launch Qoder CN with active multi-provider environment (Ctrl+L)");
            rightFlow.Controls.Add(launchButton);

            applyConfigButton = new Button
            {
                Text = "⚡ 应用配置 (免提权)",
                Size = new Size(205, 34),
                Font = new Font("Segoe UI", 9f),
                BackColor = Color.FromArgb(240, 250, 242),
                Margin = new Padding(6, 0, 0, 0)
            };
            applyConfigButton.Click += (s, e) => ApplyConfigOnly(true);
            toolTip.SetToolTip(applyConfigButton, "Save workspace & hot-reload runtime JSON with zero UAC elevation (Ctrl+S)");
            rightFlow.Controls.Add(applyConfigButton);

            primaryActionButton = new Button
            {
                Text = "正在检查状态...",
                Size = new Size(300, 34),
                Font = new Font("Segoe UI Semibold", 9.2f),
                BackColor = Color.FromArgb(230, 240, 255),
                Cursor = Cursors.Hand,
                Margin = new Padding(6, 0, 0, 0)
            };
            primaryActionButton.Click += PrimaryActionButton_Click;
            toolTip.SetToolTip(primaryActionButton, "Execute recommended action based on current state (Ctrl+Enter)");
            rightFlow.Controls.Add(primaryActionButton);
        }

        private void AppendLog(string message, Color color)
        {
            string line = string.Format("[{0}] {1}\r\n", DateTime.Now.ToString("HH:mm:ss"), message);
            outputBox.SelectionStart = outputBox.TextLength;
            outputBox.SelectionLength = 0;
            outputBox.SelectionColor = color;
            outputBox.AppendText(line);
            outputBox.ScrollToCaret();
            if (IsHandleCreated)
            {
                Application.DoEvents();
            }
        }

        private void SetBusy(bool busy, string status)
        {
            isBusy = busy;
            primaryActionButton.Enabled = !busy;
            applyConfigButton.Enabled = !busy;
            launchButton.Enabled = !busy;
            restoreButton.Enabled = !busy;
            inspectButton.Enabled = !busy;
            refreshButton.Enabled = !busy;
            browseInstallButton.Enabled = !busy;
            installText.Enabled = !busy;
            addProviderBtn.Enabled = !busy;
            UpdateProviderActionButtons();
            UseWaitCursor = busy;
            statusLabel.Text = status;
            if (IsHandleCreated)
            {
                Application.DoEvents();
            }
        }

        private void UpdateProviderActionButtons()
        {
            bool hasSel = providerListView.SelectedItems.Count > 0 && !isBusy;
            editProviderBtn.Enabled = hasSel;
            removeProviderBtn.Enabled = hasSel;
            fetchModelsBtn.Enabled = hasSel;
            testProviderBtn.Enabled = hasSel;
        }

        private void RefreshProviderList()
        {
            providerListView.BeginUpdate();
            providerListView.Items.Clear();
            foreach (ProviderItem p in workspace.providers)
            {
                ListViewItem item = new ListViewItem(p.name);
                item.Tag = p;
                item.SubItems.Add(p.baseUrl);
                item.SubItems.Add((p.models != null ? p.models.Count : 0).ToString());

                bool hasKey = SecretStore.HasProviderKey(p.id);
                item.SubItems.Add(hasKey ? "🔒 已保存 (DPAPI)" : "⚪ 未配置 Key");
                item.SubItems.Add(string.Format("{0}s / {1}s", p.firstPayloadTimeoutMs / 1000, p.streamIdleTimeoutMs / 1000));
                providerListView.Items.Add(item);
            }
            providerListView.EndUpdate();
            UpdateProviderActionButtons();
        }

        private void RefreshProviderFilterCombo()
        {
            string selected = providerFilterCombo.SelectedItem != null ? providerFilterCombo.SelectedItem.ToString() : "All Providers";
            providerFilterCombo.Items.Clear();
            providerFilterCombo.Items.Add("All Providers");
            foreach (ProviderItem p in workspace.providers)
            {
                providerFilterCombo.Items.Add(p.name);
            }
            int idx = providerFilterCombo.Items.IndexOf(selected);
            providerFilterCombo.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private void ApplyModelFilter()
        {
            if (isApplyingModelFilter) return;
            isApplyingModelFilter = true;
            try
            {
                modelCheckList.BeginUpdate();
                modelCheckList.Items.Clear();
                filteredModelList.Clear();

                string filterText = modelSearchText != null ? modelSearchText.Text.Trim() : "";
                string filterProvider = providerFilterCombo != null && providerFilterCombo.SelectedItem != null ? providerFilterCombo.SelectedItem.ToString() : "All Providers";

                List<ModelItem> allModels = workspace.GetAllModels();

                foreach (ModelItem m in allModels)
                {
                    bool matchText = string.IsNullOrEmpty(filterText) ||
                                     (m.id != null && m.id.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                     (m.displayName != null && m.displayName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0);

                    bool matchProv = filterProvider == "All Providers" ||
                                     string.Equals(m.providerName, filterProvider, StringComparison.OrdinalIgnoreCase);

                    if (matchText && matchProv)
                    {
                        filteredModelList.Add(m);
                        int idx = modelCheckList.Items.Add(m);
                        string uniqueKey = string.Format("{0}::{1}", m.providerId, m.id);
                        if (checkedModelKeys.Contains(uniqueKey) || checkedModelKeys.Contains(m.id) || m.selectedForInjection)
                        {
                            modelCheckList.SetItemChecked(idx, true);
                        }
                    }
                }
                modelCheckList.EndUpdate();
                UpdateModelCountLabel();
                ReevaluateState();
            }
            finally
            {
                isApplyingModelFilter = false;
            }
        }

        private void UpdateModelCountLabel()
        {
            List<ModelItem> all = workspace.GetAllModels();
            int checkedCount = 0;
            foreach (ModelItem m in all)
            {
                string uniqueKey = string.Format("{0}::{1}", m.providerId, m.id);
                if (checkedModelKeys.Contains(uniqueKey) || checkedModelKeys.Contains(m.id) || m.selectedForInjection)
                {
                    checkedCount++;
                }
            }
            modelCountLbl.Text = string.Format("已勾选 {0} / {1} 个模型准备注入 Qoder", checkedCount, all.Count);
        }

        private ProviderItem GetSelectedProvider()
        {
            if (providerListView.SelectedItems.Count == 0) return null;
            return providerListView.SelectedItems[0].Tag as ProviderItem;
        }

        private void AddProviderBtn_Click(object sender, EventArgs e)
        {
            using (ProviderEditForm form = new ProviderEditForm(null))
            {
                if (form.ShowDialog(this) == DialogResult.OK && form.Provider != null)
                {
                    workspace.providers.Add(form.Provider);
                    workspace.SaveToFile(workspaceFilePath);
                    RefreshProviderList();
                    RefreshProviderFilterCombo();
                    ApplyModelFilter();
                    AppendLog("Added provider: " + form.Provider.name, Color.DarkGreen);
                    ReevaluateState();
                }
            }
        }

        private void EditProviderBtn_Click(object sender, EventArgs e)
        {
            ProviderItem p = GetSelectedProvider();
            if (p == null) return;

            using (ProviderEditForm form = new ProviderEditForm(p))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    workspace.SaveToFile(workspaceFilePath);
                    RefreshProviderList();
                    RefreshProviderFilterCombo();
                    ApplyModelFilter();
                    AppendLog("Updated provider: " + p.name, Color.DarkGreen);
                    ReevaluateState();
                }
            }
        }

        private void RemoveProviderBtn_Click(object sender, EventArgs e)
        {
            ProviderItem p = GetSelectedProvider();
            if (p == null) return;

            if (MessageBox.Show(this, string.Format("确定要删除 Provider 【{0}】及其所有模型吗？", p.name), "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                SecretStore.DeleteProviderKey(p.id);
                workspace.providers.Remove(p);
                workspace.SaveToFile(workspaceFilePath);
                RefreshProviderList();
                RefreshProviderFilterCombo();
                ApplyModelFilter();
                AppendLog("Removed provider: " + p.name, Color.DarkOrange);
                ReevaluateState();
            }
        }

        private async Task FetchModelsForSelectedProvider()
        {
            ProviderItem p = GetSelectedProvider();
            if (p == null) return;

            SetBusy(true, "Fetching models for " + p.name + "...");
            AppendLog(string.Format("[DISCOVERY] Querying GET {0}/models for {1}...", p.baseUrl, p.name), Color.Navy);

            string key = SecretStore.LoadProviderKey(p.id);
            TestResult res = await Task.Run(() => UpstreamTester.FetchModels(p.baseUrl, key, true));
            SetBusy(false, "Ready");

            if (res.Success)
            {
                if (res.DiscoveredModels.Count > 0)
                {
                    int added = 0;
                    foreach (ModelItem dm in res.DiscoveredModels)
                    {
                        dm.providerId = p.id;
                        dm.providerName = p.name;
                        dm.upstreamBaseUrl = p.baseUrl;
                        dm.uiBaseUrl = p.GetEffectiveUiUrl();

                        int existingIdx = p.models.FindIndex(m => m.id.Equals(dm.id, StringComparison.OrdinalIgnoreCase));
                        if (existingIdx >= 0)
                        {
                            p.models[existingIdx] = dm;
                        }
                        else
                        {
                            p.models.Add(dm);
                            added++;
                        }
                        string uniqueKey = string.Format("{0}::{1}", p.id, dm.id);
                        checkedModelKeys.Add(uniqueKey);
                        checkedModelKeys.Add(dm.id);
                    }
                    workspace.SaveToFile(workspaceFilePath);
                    RefreshProviderList();
                    ApplyModelFilter();
                    AppendLog(string.Format("[OK] 成功从 {0} 发现 {1} 个模型 (新增 {2} 个)。", p.name, res.DiscoveredModels.Count, added), Color.DarkGreen);
                    MessageBox.Show(this, string.Format("成功从 【{0}】 发现 {1} 个模型！\n\n已自动加入模型池并勾选注入。", p.name, res.DiscoveredModels.Count), "模型发现成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    AppendLog(string.Format("[INFO] {0} 连通正常，但端点未返回模型列表。", p.name), Color.DarkSlateGray);
                    MessageBox.Show(this, "端点连通正常，但未返回模型列表，您可点击【手动添加模型】补充。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                AppendLog(string.Format("[FAIL] 拉取模型失败 ({0}): {1}", p.name, res.Message), Color.Red);
                MessageBox.Show(this, string.Format("拉取模型失败: {0}", res.Message), "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            ReevaluateState();
        }

        private async Task TestSelectedProviderConn()
        {
            ProviderItem p = GetSelectedProvider();
            if (p == null) return;

            SetBusy(true, "Testing connection for " + p.name + "...");
            AppendLog(string.Format("[TEST] Testing connection to {0} ({1})...", p.baseUrl, p.name), Color.Navy);

            string key = SecretStore.LoadProviderKey(p.id);
            TestResult res = await Task.Run(() => UpstreamTester.TestConnection(p.baseUrl, key));
            SetBusy(false, "Ready");

            if (res.Success)
            {
                AppendLog(string.Format("[OK] {0} 连接正常: {1}", p.name, res.Message), Color.DarkGreen);
                MessageBox.Show(this, string.Format("Provider 【{0}】 连接正常！\n\n{1}", p.name, res.Message), "连通成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                AppendLog(string.Format("[FAIL] {0} 连接失败: {1}", p.name, res.Message), Color.Red);
                MessageBox.Show(this, string.Format("Provider 【{0}】 连接失败: {1}", p.name, res.Message), "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ImportProfileBtn_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.InitialDirectory = configsDir;
                dlg.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
                dlg.Title = "选择要导入的配置 Profile";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        ProviderConfig oldCfg = ProviderConfig.LoadFromFile(dlg.FileName);
                        string pName = Path.GetFileNameWithoutExtension(dlg.FileName);
                        GatewayWorkspace imported = GatewayWorkspace.FromSingleProfile(oldCfg, pName);
                        foreach (ProviderItem ip in imported.providers)
                        {
                            workspace.providers.Add(ip);
                            if (!string.IsNullOrWhiteSpace(oldCfg.apiKey))
                            {
                                SecretStore.SaveProviderKey(ip.id, oldCfg.apiKey);
                            }
                        }
                        workspace.SaveToFile(workspaceFilePath);
                        RefreshProviderList();
                        RefreshProviderFilterCombo();
                        ApplyModelFilter();
                        AppendLog("Imported profile: " + dlg.FileName, Color.DarkGreen);
                        ReevaluateState();
                    }
                    catch (Exception ex)
                    {
                        AppendLog("[ERROR] Failed to import: " + ex.Message, Color.Red);
                        MessageBox.Show(this, "导入失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void AddModelButton_Click(object sender, EventArgs e)
        {
            if (workspace.providers.Count == 0)
            {
                MessageBox.Show(this, "请先添加至少一个 AI Provider。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tabControl.SelectedTab = tabProviders;
                return;
            }

            ProviderItem targetProvider = workspace.providers[0];
            string filterProvider = providerFilterCombo.SelectedItem != null ? providerFilterCombo.SelectedItem.ToString() : "All Providers";
            if (filterProvider != "All Providers")
            {
                ProviderItem match = workspace.providers.Find(p => p.name.Equals(filterProvider, StringComparison.OrdinalIgnoreCase));
                if (match != null) targetProvider = match;
            }

            using (ModelEditForm form = new ModelEditForm(null))
            {
                if (form.ShowDialog(this) == DialogResult.OK && form.ResultModel != null)
                {
                    form.ResultModel.providerId = targetProvider.id;
                    form.ResultModel.providerName = targetProvider.name;
                    form.ResultModel.upstreamBaseUrl = targetProvider.baseUrl;
                    form.ResultModel.uiBaseUrl = targetProvider.GetEffectiveUiUrl();
                    form.ResultModel.selectedForInjection = true;

                    targetProvider.models.Add(form.ResultModel);
                    string uniqueKey = string.Format("{0}::{1}", targetProvider.id, form.ResultModel.id);
                    checkedModelKeys.Add(uniqueKey);
                    checkedModelKeys.Add(form.ResultModel.id);

                    workspace.SaveToFile(workspaceFilePath);
                    RefreshProviderList();
                    ApplyModelFilter();
                    AppendLog(string.Format("Added custom model: {0} ({1})", form.ResultModel.id, targetProvider.name), Color.DarkGreen);
                    ReevaluateState();
                }
            }
        }

        private void EditModelButton_Click(object sender, EventArgs e)
        {
            int idx = modelCheckList.SelectedIndex;
            if (idx < 0 || idx >= modelCheckList.Items.Count)
            {
                MessageBox.Show(this, "请从列表中选择一个模型进行编辑（或双击该模型）。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ModelItem oldItem = (ModelItem)modelCheckList.Items[idx];
            using (ModelEditForm form = new ModelEditForm(oldItem))
            {
                if (form.ShowDialog(this) == DialogResult.OK && form.ResultModel != null)
                {
                    ProviderItem parent = workspace.providers.Find(p => p.id == oldItem.providerId);
                    if (parent != null)
                    {
                        int mIdx = parent.models.FindIndex(m => m.id.Equals(oldItem.id, StringComparison.OrdinalIgnoreCase));
                        if (mIdx >= 0)
                        {
                            form.ResultModel.providerId = parent.id;
                            form.ResultModel.providerName = parent.name;
                            form.ResultModel.upstreamBaseUrl = parent.baseUrl;
                            form.ResultModel.uiBaseUrl = parent.GetEffectiveUiUrl();
                            form.ResultModel.selectedForInjection = oldItem.selectedForInjection;
                            parent.models[mIdx] = form.ResultModel;
                        }
                    }

                    workspace.SaveToFile(workspaceFilePath);
                    ApplyModelFilter();
                    AppendLog("Updated model specs: " + form.ResultModel.id, Color.DarkGreen);
                    ReevaluateState();
                }
            }
        }

        private void RemoveModelButton_Click(object sender, EventArgs e)
        {
            int idx = modelCheckList.SelectedIndex;
            if (idx < 0 || idx >= modelCheckList.Items.Count) return;

            ModelItem item = (ModelItem)modelCheckList.Items[idx];
            ProviderItem parent = workspace.providers.Find(p => p.id == item.providerId);
            if (parent != null)
            {
                parent.models.RemoveAll(m => m.id.Equals(item.id, StringComparison.OrdinalIgnoreCase));
            }
            string uniqueKey = string.Format("{0}::{1}", item.providerId, item.id);
            checkedModelKeys.Remove(uniqueKey);
            checkedModelKeys.Remove(item.id);

            workspace.SaveToFile(workspaceFilePath);
            RefreshProviderList();
            ApplyModelFilter();
            AppendLog("Removed model: " + item.id, Color.DimGray);
            ReevaluateState();
        }

        private void BrowseInstallButton_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.SelectedPath = installText.Text;
                dlg.Description = "Select Qoder CN installation directory:";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    installText.Text = dlg.SelectedPath;
                    ReevaluateState();
                }
            }
        }

        private void CopyLogButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(outputBox.Text))
            {
                try
                {
                    Clipboard.SetText(outputBox.Text);
                    AppendLog("Activity log copied to clipboard.", Color.DarkGreen);
                }
                catch (Exception ex)
                {
                    AppendLog("[WARN] Failed to copy log to clipboard: " + ex.Message, Color.DarkOrange);
                }
            }
        }

        private bool IsConfigInSync(out string reason)
        {
            reason = "";
            if (!File.Exists(runtimeConfigPath))
            {
                reason = "Runtime configuration file does not exist.";
                return false;
            }

            try
            {
                ProviderConfig current = workspace.CompileToRuntimeConfig(checkedModelKeys);
                ProviderConfig active = ProviderConfig.LoadFromFile(runtimeConfigPath);

                if (current.models.Count != active.models.Count)
                {
                    reason = "Injected model count mismatch.";
                    return false;
                }

                for (int i = 0; i < current.models.Count; i++)
                {
                    if (current.models[i].id != active.models[i].id ||
                        current.models[i].displayName != active.models[i].displayName ||
                        current.models[i].upstreamBaseUrl != active.models[i].upstreamBaseUrl ||
                        current.models[i].vision != active.models[i].vision ||
                        current.models[i].reasoning != active.models[i].reasoning ||
                        current.models[i].tools != active.models[i].tools ||
                        current.models[i].maxInputTokens != active.models[i].maxInputTokens ||
                        current.models[i].maxOutputTokens != active.models[i].maxOutputTokens ||
                        current.models[i].maxTokensField != active.models[i].maxTokensField)
                    {
                        reason = "Model definition mismatch: " + current.models[i].id;
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                reason = "Error checking sync state: " + ex.Message;
                return false;
            }
        }

        public GuiState EvaluateCurrentState(out string detailMessage)
        {
            detailMessage = "";
            string inst = installText.Text.Trim();
            if (string.IsNullOrEmpty(inst) || !Directory.Exists(inst) || !File.Exists(Path.Combine(inst, PatcherEngine.AsarRelativePath)))
            {
                detailMessage = "Qoder installation directory is invalid or app.asar is missing.";
                return GuiState.InvalidDirectory;
            }

            TargetState targetState;
            try
            {
                targetState = PatcherEngine.GetTargetState(inst);
            }
            catch (Exception ex)
            {
                detailMessage = "Failed to inspect runtime: " + ex.Message;
                return GuiState.IncompatibleRuntime;
            }

            if (!targetState.RuntimePatched && !targetState.PreviousRuntimePatched &&
                !string.Equals(targetState.RuntimeSha256, PatcherEngine.SupportedRuntimeSha256, StringComparison.OrdinalIgnoreCase))
            {
                detailMessage = "Unknown runtime binary checksum. Inspect before continuing.";
                return GuiState.IncompatibleRuntime;
            }

            if (workspace.providers.Count == 0)
            {
                detailMessage = "No AI Provider configured. Please add a provider.";
                return GuiState.IncompleteProfile;
            }

            List<ModelItem> injected = workspace.CompileToRuntimeConfig(checkedModelKeys).models;
            if (injected.Count == 0)
            {
                detailMessage = "No models selected. Please check at least 1 model for injection.";
                return GuiState.IncompleteProfile;
            }

            bool isQoderRunning = PatcherEngine.IsQoderRunning(inst);

            if (!targetState.RuntimePatched && !targetState.PreviousRuntimePatched)
            {
                if (isQoderRunning)
                {
                    detailMessage = "Qoder CN is currently running. Please close it before installing the patch.";
                    return GuiState.RunningConflict;
                }
                detailMessage = "Official baseline detected. Ready to install v3.2.0 patch.";
                return GuiState.ReadyToPatchBaseline;
            }

            if (targetState.PreviousRuntimePatched)
            {
                if (isQoderRunning)
                {
                    detailMessage = "Qoder CN is currently running. Please close it before upgrading the patch.";
                    return GuiState.RunningConflict;
                }
                detailMessage = "Older patch detected. Ready to upgrade to v3.2.0.";
                return GuiState.ReadyToUpgradeOlder;
            }

            // Runtime is patched with v3.2.0
            string syncReason;
            bool inSync = IsConfigInSync(out syncReason);
            if (!inSync)
            {
                detailMessage = "Configuration modified. Click 'Apply Config' to hot-reload without UAC.";
                return GuiState.ConfigOutOfSync;
            }

            if (isQoderRunning)
            {
                detailMessage = "Qoder CN is running with active v3.2.0 configuration.";
                return GuiState.RunningHealthy;
            }

            detailMessage = "v3.2.0 runtime is active and config is synced. Ready to launch.";
            return GuiState.ReadyToLaunch;
        }

        public void ReevaluateState(bool logChanges = false)
        {
            string detail;
            GuiState newState = EvaluateCurrentState(out detail);
            currentState = newState;

            UpdateUiForState(newState, detail);
            if (logChanges && !string.IsNullOrEmpty(detail))
            {
                AppendLog("[STATE] " + detail, Color.DarkSlateGray);
            }
        }

        private void UpdateUiForState(GuiState state, string detail)
        {
            switch (state)
            {
                case GuiState.InvalidDirectory:
                    statusBadgeLbl.Text = "🔴 目录无效";
                    statusBadgeLbl.ForeColor = Color.Red;
                    primaryActionButton.Text = "📁 请选择 Qoder 安装目录";
                    primaryActionButton.BackColor = Color.FromArgb(255, 230, 230);
                    break;

                case GuiState.IncompatibleRuntime:
                    statusBadgeLbl.Text = "🔴 运行时不兼容";
                    statusBadgeLbl.ForeColor = Color.Red;
                    primaryActionButton.Text = "🔍 查看运行时诊断";
                    primaryActionButton.BackColor = Color.FromArgb(255, 230, 230);
                    break;

                case GuiState.IncompleteProfile:
                    statusBadgeLbl.Text = "🟡 配置不完整";
                    statusBadgeLbl.ForeColor = Color.DarkOrange;
                    primaryActionButton.Text = "✏️ 完善 Provider 与模型选择";
                    primaryActionButton.BackColor = Color.FromArgb(255, 245, 220);
                    break;

                case GuiState.RunningConflict:
                    statusBadgeLbl.Text = "🟠 Qoder 运行中 (需关闭)";
                    statusBadgeLbl.ForeColor = Color.DarkOrange;
                    primaryActionButton.Text = "⏹️ 关闭 Qoder 后继续";
                    primaryActionButton.BackColor = Color.FromArgb(255, 240, 210);
                    break;

                case GuiState.ReadyToPatchBaseline:
                    statusBadgeLbl.Text = "🔵 准备安装 v3.2.0";
                    statusBadgeLbl.ForeColor = Color.RoyalBlue;
                    primaryActionButton.Text = "🛡️ 安装 v3.2.0 (需要管理员提权)";
                    primaryActionButton.BackColor = Color.FromArgb(220, 240, 255);
                    break;

                case GuiState.ReadyToUpgradeOlder:
                    statusBadgeLbl.Text = "🟠 发现旧版补丁 (准备升级)";
                    statusBadgeLbl.ForeColor = Color.DarkOrange;
                    primaryActionButton.Text = "🛡️ 升级至 v3.2.0 (需要管理员提权)";
                    primaryActionButton.BackColor = Color.FromArgb(255, 240, 220);
                    break;

                case GuiState.ConfigOutOfSync:
                    statusBadgeLbl.Text = "⚡ 待应用配置更改";
                    statusBadgeLbl.ForeColor = Color.Teal;
                    primaryActionButton.Text = "⚡ 应用配置 (免 UAC 提权)";
                    primaryActionButton.BackColor = Color.FromArgb(215, 250, 230);
                    break;

                case GuiState.ReadyToLaunch:
                    statusBadgeLbl.Text = "🟢 v3.2.0 已就绪";
                    statusBadgeLbl.ForeColor = Color.ForestGreen;
                    primaryActionButton.Text = "🚀 启动 Qoder CN";
                    primaryActionButton.BackColor = Color.FromArgb(225, 248, 225);
                    break;

                case GuiState.RunningHealthy:
                    statusBadgeLbl.Text = "🟢 Qoder CN 运行中";
                    statusBadgeLbl.ForeColor = Color.ForestGreen;
                    primaryActionButton.Text = "✨ Qoder 运行中 (点击重新启动)";
                    primaryActionButton.BackColor = Color.FromArgb(235, 248, 235);
                    break;

                default:
                    statusBadgeLbl.Text = "⚪ 检查中...";
                    statusBadgeLbl.ForeColor = Color.Gray;
                    primaryActionButton.Text = "检查状态中...";
                    break;
            }
        }

        private void PrimaryActionButton_Click(object sender, EventArgs e)
        {
            switch (currentState)
            {
                case GuiState.InvalidDirectory:
                    browseInstallButton.PerformClick();
                    break;

                case GuiState.IncompatibleRuntime:
                    PerformInspect();
                    break;

                case GuiState.IncompleteProfile:
                    if (workspace.providers.Count == 0)
                    {
                        tabControl.SelectedTab = tabProviders;
                        addProviderBtn.PerformClick();
                    }
                    else
                    {
                        tabControl.SelectedTab = tabModels;
                        MessageBox.Show(this, "请在【模型池与注入选择】中勾选至少 1 个要注入 Qoder 的模型。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    break;

                case GuiState.RunningConflict:
                    PromptCloseQoder();
                    break;

                case GuiState.ReadyToPatchBaseline:
                case GuiState.ReadyToUpgradeOlder:
                    ApplyPatchWithElevation();
                    break;

                case GuiState.ConfigOutOfSync:
                    ApplyConfigOnly(true);
                    break;

                case GuiState.ReadyToLaunch:
                    LaunchQoder();
                    break;

                case GuiState.RunningHealthy:
                    if (MessageBox.Show(this, "Qoder CN 当前正在运行中。是否重新启动 Qoder 以应用最新配置与环境变量？", "重新启动 Qoder", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        KillQoderProcess();
                        LaunchQoder();
                    }
                    break;
            }
        }

        private void PromptCloseQoder()
        {
            if (MessageBox.Show(this, "Qoder CN 正在运行中并锁定了运行时文件。是否立即终止 Qoder CN 进程？", "关闭 Qoder", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                KillQoderProcess();
                ReevaluateState(true);
            }
        }

        private void KillQoderProcess()
        {
            try
            {
                Process[] procs = Process.GetProcessesByName("Qoder CN");
                foreach (Process p in procs) { try { p.Kill(); p.WaitForExit(3000); } catch { } }
                procs = Process.GetProcessesByName("Qoder");
                foreach (Process p in procs) { try { p.Kill(); p.WaitForExit(3000); } catch { } }
                AppendLog("Terminated running Qoder CN processes.", Color.DarkOrange);
            }
            catch (Exception ex)
            {
                AppendLog("[WARN] Could not close Qoder process: " + ex.Message, Color.DarkOrange);
            }
        }

        public void ApplyConfigOnly(bool notifyUser = false)
        {
            try
            {
                ProviderConfig runtimeCfg = workspace.CompileToRuntimeConfig(checkedModelKeys);
                if (runtimeCfg.models.Count == 0)
                {
                    MessageBox.Show(this, "请在模型池中至少勾选 1 个要注入的模型。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                workspace.SaveToFile(workspaceFilePath);
                runtimeCfg.SaveToFile(runtimeConfigPath);

                AppendLog(string.Format("[HOT-RELOAD] 成功热重载配置至 {0} (免 UAC 提权，已注入 {1} 个模型)。", Path.GetFileName(runtimeConfigPath), runtimeCfg.models.Count), Color.DarkGreen);

                ReevaluateState(true);
                if (notifyUser)
                {
                    MessageBox.Show(this, string.Format("配置已成功保存并免提权热重载！\n\n已成功注入 {0} 个模型至 Qoder CN。", runtimeCfg.models.Count), "配置已生效", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                AppendLog("[ERROR] Failed to apply config: " + ex.Message, Color.Red);
                MessageBox.Show(this, "Failed to apply config: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyPatchWithElevation()
        {
            try
            {
                string inst = installText.Text.Trim();
                ApplyConfigOnly(false);

                SetBusy(true, "Installing v3.2.0 patch...");
                AppendLog("[INFO] Applying v3.2.0 runtime patch with per-operation UAC...", Color.Navy);

                ElevationProtocol.Invoke("apply", inst, backupRoot);

                SetBusy(false, "Ready");
                ReevaluateState(true);
                AppendLog("[SUCCESS] Qoder CN v3.2.0 patch installed successfully!", Color.DarkGreen);
                MessageBox.Show(this, "Qoder CN v3.2.0 patch installed successfully!\n\nAll API Keys remain DPAPI-encrypted and are securely passed into memory only when Qoder is launched from this manager.", "Installed Successfully", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetBusy(false, "Ready");
                AppendLog("[ERROR] Apply failed: " + ex.Message, Color.Red);
                MessageBox.Show(this, "Apply failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestoreButton_Click(object sender, EventArgs e)
        {
            try
            {
                string inst = installText.Text.Trim();
                SetBusy(true, "Restoring runtime backup...");
                AppendLog("[INFO] Restoring runtime from latest verified backup...", Color.Navy);

                ElevationProtocol.Invoke("restore", inst, backupRoot);

                SetBusy(false, "Ready");
                ReevaluateState(true);
                AppendLog("[SUCCESS] Qoder CN runtime restored to original state.", Color.DarkGreen);
                MessageBox.Show(this, "Qoder CN runtime restored to original backup state.", "Restored", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetBusy(false, "Ready");
                AppendLog("[ERROR] Restore failed: " + ex.Message, Color.Red);
                MessageBox.Show(this, "Restore failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LaunchQoder()
        {
            string inst = installText.Text.Trim();
            if (PatcherEngine.IsQoderRunning(inst))
            {
                MessageBox.Show(this, "Qoder CN is already running. Please close it first or click 'Restart Qoder' to launch a fresh instance.", "Qoder Already Running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string exePath = Path.Combine(inst, "Qoder CN.exe");
            if (!File.Exists(exePath)) exePath = Path.Combine(inst, "Qoder.exe");

            if (File.Exists(exePath))
            {
                try
                {
                    // Ensure runtime config is synced before launch
                    if (!File.Exists(runtimeConfigPath))
                    {
                        ApplyConfigOnly(false);
                    }

                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = inst,
                        UseShellExecute = false
                    };
                    startInfo.EnvironmentVariables["QODER_CN_CUSTOM_PROVIDER_CONFIG"] = runtimeConfigPath;

                    // Decrypt and pass DPAPI keys for all providers in workspace
                    string primaryKey = "";
                    foreach (ProviderItem p in workspace.providers)
                    {
                        string pKey = SecretStore.LoadProviderKey(p.id);
                        if (!string.IsNullOrWhiteSpace(pKey))
                        {
                            startInfo.EnvironmentVariables["QODER_CN_KEY_" + p.id] = pKey;
                            if (string.IsNullOrEmpty(primaryKey)) primaryKey = pKey;
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(primaryKey))
                    {
                        startInfo.EnvironmentVariables["QODER_CN_CUSTOM_PROVIDER_API_KEY"] = primaryKey;
                    }

                    Process.Start(startInfo);
                    AppendLog("Launched Qoder CN with multi-provider environment: " + exePath, Color.Green);
                    ReevaluateState(true);
                }
                catch (Exception ex)
                {
                    AppendLog("[ERROR] Failed to launch Qoder CN: " + ex.Message, Color.Red);
                }
            }
            else
            {
                MessageBox.Show(this, "Qoder executable not found in: " + inst, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PerformInspect()
        {
            try
            {
                string inst = installText.Text.Trim();
                TargetState state = PatcherEngine.GetTargetState(inst);

                AppendLog("--- Qoder CN Inspection (v3.2.0-beta) ---", Color.Black);
                AppendLog(string.Format("Install directory: {0}", Path.GetFullPath(inst)), Color.Black);
                AppendLog(string.Format("Runtime patched  : {0} (Marker: {1})", state.RuntimePatched, PatcherEngine.PatchMarker), Color.Black);
                AppendLog(string.Format("Older patch      : {0}", state.PreviousRuntimePatched), Color.Black);
                AppendLog(string.Format("app.asar intact  : {0}", state.AppAsarUnmodified), Color.Black);
                AppendLog(string.Format("Runtime SHA-256  : {0}", state.RuntimeSha256), Color.Black);
                AppendLog(string.Format("Anchors matched  : import={0}, converter={1}, url={2}, catalog={3}, route={4}",
                    state.ImportAnchorCount, state.ConverterAnchorCount, state.ModelUrlAnchorCount, state.CatalogAnchorCount, state.InferenceRouteAnchorCount), Color.Black);

                ReevaluateState(false);
            }
            catch (Exception ex)
            {
                AppendLog("[ERROR] Inspect failed: " + ex.Message, Color.Red);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                if (applyConfigButton.Enabled)
                {
                    ApplyConfigOnly(true);
                    return true;
                }
            }
            else if (keyData == Keys.F5)
            {
                if (refreshButton.Enabled)
                {
                    RefreshProviderList();
                    RefreshProviderFilterCombo();
                    ApplyModelFilter();
                    ReevaluateState(true);
                    return true;
                }
            }
            else if (keyData == (Keys.Control | Keys.L))
            {
                if (launchButton.Enabled)
                {
                    LaunchQoder();
                    return true;
                }
            }
            else if (keyData == (Keys.Control | Keys.Return) || keyData == (Keys.Control | Keys.Enter))
            {
                if (primaryActionButton.Enabled)
                {
                    primaryActionButton.PerformClick();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
