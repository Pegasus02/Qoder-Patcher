using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QoderCN.GatewayManager
{
    public class ProviderEditForm : Form
    {
        public ProviderItem Provider { get; private set; }
        public List<ModelItem> DiscoveredModels { get; private set; }
        public bool DiscoveredAny { get; private set; }

        private TextBox nameText;
        private TextBox baseUrlText;
        private TextBox uiUrlText;
        private TextBox apiKeyText;
        private Button toggleKeyBtn;
        private Button pasteKeyBtn;
        private Button clearKeyBtn;
        private Label keyStatusLabel;

        private NumericUpDown firstPayloadNumeric;
        private NumericUpDown streamIdleNumeric;
        private Label statusLabel;
        private Button testFetchBtn;
        private Button okBtn;
        private Button cancelBtn;

        public ProviderEditForm(ProviderItem provider = null)
        {
            if (provider != null)
            {
                Provider = provider;
            }
            else
            {
                Provider = new ProviderItem();
            }
            DiscoveredModels = new List<ModelItem>();
            DiscoveredAny = false;

            InitializeComponent();
            PopulateData();
        }

        private void InitializeComponent()
        {
            bool isNew = string.IsNullOrEmpty(Provider.name) || Provider.name == "New Provider";
            this.Text = isNew ? "添加 AI 提供方 (Add AI Provider)" : "编辑 AI 提供方 - " + Provider.name;
            this.Size = new Size(680, 580);
            this.MinimumSize = new Size(640, 540);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(20, 16, 20, 10);
            layout.ColumnCount = 2;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            int row = 0;

            // 1. Provider Name
            Label lblName = new Label { Text = "Provider 名称:", Anchor = AnchorStyles.Left, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Margin = new Padding(0, 7, 0, 7) };
            layout.Controls.Add(lblName, 0, row);
            nameText = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 8) };
            layout.Controls.Add(nameText, 1, row++);

            // 2. Base URL
            Label lblBase = new Label { Text = "Base URL (v1 地址):", Anchor = AnchorStyles.Left, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Margin = new Padding(0, 7, 0, 7) };
            layout.Controls.Add(lblBase, 0, row);
            baseUrlText = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 8) };
            layout.Controls.Add(baseUrlText, 1, row++);

            // 3. UI Base URL
            Label lblUi = new Label { Text = "UI Base URL (可选):", Anchor = AnchorStyles.Left, AutoSize = true, Margin = new Padding(0, 7, 0, 7) };
            layout.Controls.Add(lblUi, 0, row);
            uiUrlText = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 8) };
            layout.Controls.Add(uiUrlText, 1, row++);

            // 4. API Key (DPAPI Encrypted)
            Label lblKey = new Label { Text = "API Key (安全加密):", Anchor = AnchorStyles.Left, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Margin = new Padding(0, 7, 0, 7) };
            layout.Controls.Add(lblKey, 0, row);

            Panel keyContainer = new Panel { Dock = DockStyle.Fill, Height = 66, Margin = new Padding(0, 2, 0, 6) };
            
            Panel keyInputRow = new Panel { Dock = DockStyle.Top, Height = 30 };
            
            FlowLayoutPanel keyButtonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            toggleKeyBtn = new Button
            {
                Text = "👁️ 显示",
                Size = new Size(62, 26),
                Margin = new Padding(4, 0, 0, 0)
            };
            toggleKeyBtn.Click += (s, e) =>
            {
                apiKeyText.UseSystemPasswordChar = !apiKeyText.UseSystemPasswordChar;
                toggleKeyBtn.Text = apiKeyText.UseSystemPasswordChar ? "👁️ 显示" : "🔒 隐藏";
            };

            pasteKeyBtn = new Button
            {
                Text = "📋 粘贴",
                Size = new Size(58, 26),
                Margin = new Padding(4, 0, 0, 0)
            };
            pasteKeyBtn.Click += (s, e) =>
            {
                if (Clipboard.ContainsText())
                {
                    apiKeyText.Text = Clipboard.GetText().Trim();
                }
            };

            clearKeyBtn = new Button
            {
                Text = "🧹 清空",
                Size = new Size(58, 26),
                Margin = new Padding(4, 0, 0, 0)
            };
            clearKeyBtn.Click += (s, e) => { apiKeyText.Text = ""; };

            keyButtonsPanel.Controls.Add(toggleKeyBtn);
            keyButtonsPanel.Controls.Add(pasteKeyBtn);
            keyButtonsPanel.Controls.Add(clearKeyBtn);

            apiKeyText = new TextBox
            {
                UseSystemPasswordChar = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 6, 0)
            };
            apiKeyText.TextChanged += (s, e) => UpdateKeyStatusLabel();

            keyInputRow.Controls.Add(apiKeyText);
            keyInputRow.Controls.Add(keyButtonsPanel);

            keyStatusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                Font = new Font("Segoe UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft
            };

            keyContainer.Controls.Add(keyInputRow);
            keyContainer.Controls.Add(keyStatusLabel);
            layout.Controls.Add(keyContainer, 1, row++);

            // 5. Timeouts
            Label lblTimeout = new Label { Text = "超时设置 (毫秒):", Anchor = AnchorStyles.Left, AutoSize = true, Margin = new Padding(0, 7, 0, 7) };
            layout.Controls.Add(lblTimeout, 0, row);
            
            FlowLayoutPanel timeoutPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 34, WrapContents = false, Margin = new Padding(0, 4, 0, 8) };
            timeoutPanel.Controls.Add(new Label { Text = "首包响应:", AutoSize = true, Margin = new Padding(0, 4, 4, 0) });
            firstPayloadNumeric = new NumericUpDown { Minimum = 1000, Maximum = 600000, Increment = 10000, Width = 95 };
            timeoutPanel.Controls.Add(firstPayloadNumeric);
            timeoutPanel.Controls.Add(new Label { Text = "ms    流式空闲:", AutoSize = true, Margin = new Padding(12, 4, 4, 0) });
            streamIdleNumeric = new NumericUpDown { Minimum = 0, Maximum = 600000, Increment = 10000, Width = 95 };
            timeoutPanel.Controls.Add(streamIdleNumeric);
            timeoutPanel.Controls.Add(new Label { Text = "ms", AutoSize = true, Margin = new Padding(4, 4, 0, 0) });
            layout.Controls.Add(timeoutPanel, 1, row++);

            // 6. Test & Discovery
            Label lblProbe = new Label { Text = "连通性与模型探测:", Anchor = AnchorStyles.Left, AutoSize = true, Margin = new Padding(0, 7, 0, 7) };
            layout.Controls.Add(lblProbe, 0, row);
            
            Panel testPanel = new Panel { Dock = DockStyle.Fill, Height = 80, Margin = new Padding(0, 4, 0, 4) };
            testFetchBtn = new Button
            {
                Text = "⚡ 测试连通性并拉取模型 (/models)",
                Location = new Point(0, 0),
                Size = new Size(270, 32),
                BackColor = Color.FromArgb(235, 245, 255),
                FlatStyle = FlatStyle.System
            };
            testFetchBtn.Click += async (s, e) => await PerformTestAndFetch();
            
            statusLabel = new Label
            {
                Text = "就绪：点击上方按钮可验证连接并自动获取模型列表。",
                Location = new Point(2, 38),
                Size = new Size(470, 38),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5F)
            };
            testPanel.Controls.Add(testFetchBtn);
            testPanel.Controls.Add(statusLabel);
            layout.Controls.Add(testPanel, 1, row++);

            // Bottom Action Bar
            FlowLayoutPanel btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 52,
                Padding = new Padding(18, 10, 18, 10),
                BackColor = Color.FromArgb(240, 242, 245)
            };
            cancelBtn = new Button { Text = "取消 (Cancel)", DialogResult = DialogResult.Cancel, Size = new Size(100, 32), Margin = new Padding(8, 0, 0, 0) };
            okBtn = new Button { Text = "保存 Provider", Size = new Size(130, 32), BackColor = Color.FromArgb(16, 120, 50), ForeColor = Color.White, FlatStyle = FlatStyle.System, Margin = new Padding(8, 0, 0, 0) };
            okBtn.Click += (s, e) => SaveAndClose();

            btnPanel.Controls.Add(cancelBtn);
            btnPanel.Controls.Add(okBtn);

            this.Controls.Add(layout);
            this.Controls.Add(btnPanel);
            this.AcceptButton = okBtn;
            this.CancelButton = cancelBtn;
        }

        private void PopulateData()
        {
            nameText.Text = Provider.name ?? "New Provider";
            baseUrlText.Text = Provider.baseUrl ?? "http://127.0.0.1:11434/v1";
            uiUrlText.Text = Provider.uiBaseUrl ?? "";
            firstPayloadNumeric.Value = Provider.firstPayloadTimeoutMs > 0 ? Provider.firstPayloadTimeoutMs : 180000;
            streamIdleNumeric.Value = Provider.streamIdleTimeoutMs >= 0 ? Provider.streamIdleTimeoutMs : 300000;

            string savedKey = SecretStore.LoadProviderKey(Provider.id);
            if (!string.IsNullOrEmpty(savedKey))
            {
                apiKeyText.Text = savedKey;
            }
            UpdateKeyStatusLabel();
        }

        private void UpdateKeyStatusLabel()
        {
            bool hasKey = !string.IsNullOrWhiteSpace(apiKeyText.Text);
            if (hasKey)
            {
                keyStatusLabel.Text = "🔒 DPAPI 加密保护：保存时将安全加密存入 Windows 凭据区，磁盘不留明文。";
                keyStatusLabel.ForeColor = Color.FromArgb(16, 120, 50);
            }
            else
            {
                keyStatusLabel.Text = "⚪ 未设置密钥 (适用于本地 Ollama 或无需鉴权的网关)";
                keyStatusLabel.ForeColor = Color.DimGray;
            }
        }

        private async Task PerformTestAndFetch()
        {
            string url = baseUrlText.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                statusLabel.Text = "❌ 请先填写 Base URL。";
                statusLabel.ForeColor = Color.Red;
                return;
            }

            string key = apiKeyText.Text.Trim();

            testFetchBtn.Enabled = false;
            statusLabel.Text = "正在连接并拉取模型: " + url + " ...";
            statusLabel.ForeColor = Color.DarkBlue;

            TestResult res = await Task.Run(() => UpstreamTester.FetchModels(url, key, true));
            testFetchBtn.Enabled = true;

            if (res.Success)
            {
                if (res.DiscoveredModels.Count > 0)
                {
                    DiscoveredModels = res.DiscoveredModels;
                    DiscoveredAny = true;
                    statusLabel.Text = string.Format("✅ 连通成功！已发现 {0} 个可用模型 ({1}ms)", res.DiscoveredModels.Count, res.ElapsedMs);
                    statusLabel.ForeColor = Color.FromArgb(16, 120, 50);
                }
                else
                {
                    statusLabel.Text = string.Format("✅ 连通正常 (HTTP {0})，未返回标准模型列表，可稍后手动添加模型。", res.StatusCode);
                    statusLabel.ForeColor = Color.FromArgb(16, 120, 50);
                }
            }
            else
            {
                statusLabel.Text = string.Format("❌ 连接失败: {0}", res.Message);
                statusLabel.ForeColor = Color.Red;
            }
        }

        private void SaveAndClose()
        {
            string name = nameText.Text.Trim();
            string baseUrl = baseUrlText.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "请输入 Provider 名称。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nameText.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                MessageBox.Show(this, "请输入 Provider Base URL。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                baseUrlText.Focus();
                return;
            }

            Provider.name = name;
            Provider.baseUrl = baseUrl;
            Provider.uiBaseUrl = uiUrlText.Text.Trim();
            Provider.firstPayloadTimeoutMs = (int)firstPayloadNumeric.Value;
            Provider.streamIdleTimeoutMs = (int)streamIdleNumeric.Value;

            string keyToSave = apiKeyText.Text.Trim();
            if (!string.IsNullOrWhiteSpace(keyToSave))
            {
                SecretStore.SaveProviderKey(Provider.id, keyToSave);
            }
            else
            {
                SecretStore.DeleteProviderKey(Provider.id);
            }

            if (DiscoveredAny && DiscoveredModels != null && DiscoveredModels.Count > 0)
            {
                foreach (ModelItem dm in DiscoveredModels)
                {
                    dm.providerId = Provider.id;
                    dm.providerName = Provider.name;
                    dm.upstreamBaseUrl = Provider.baseUrl;
                    dm.uiBaseUrl = Provider.GetEffectiveUiUrl();

                    int existingIdx = Provider.models.FindIndex(m => m.id.Equals(dm.id, StringComparison.OrdinalIgnoreCase));
                    if (existingIdx >= 0)
                    {
                        Provider.models[existingIdx] = dm;
                    }
                    else
                    {
                        Provider.models.Add(dm);
                    }
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
