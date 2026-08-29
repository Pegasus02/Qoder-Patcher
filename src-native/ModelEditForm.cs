using System;
using System.Drawing;
using System.Windows.Forms;

namespace QoderCN.GatewayManager
{
    public class ModelEditForm : Form
    {
        public ModelItem ResultModel { get; private set; }

        private TextBox txtId;
        private TextBox txtName;
        private NumericUpDown numIn;
        private NumericUpDown numOut;
        private ComboBox cmbField;
        private CheckBox chkTools;
        private CheckBox chkReason;
        private CheckBox chkVision;

        public ModelEditForm(ModelItem initial = null)
        {
            bool isEdit = initial != null;
            Text = isEdit ? string.Format("编辑模型属性 (Edit Model): {0}", initial.id) : "添加新模型 (Add New Model)";
            Size = new Size(560, 530);
            MinimumSize = new Size(520, 490);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(248, 249, 250);
            AutoScaleMode = AutoScaleMode.Dpi;

            TableLayoutPanel mainLayout = new TableLayoutPanel();
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.Padding = new Padding(18, 14, 18, 10);
            mainLayout.ColumnCount = 1;
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // 1. Model ID
            Label lblId = new Label { Text = "Model ID (API 标识符，如 deepseek-chat, gpt-4o):", AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 4) };
            mainLayout.Controls.Add(lblId);

            txtId = new TextBox { Text = isEdit ? initial.id : "", Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 10) };
            mainLayout.Controls.Add(txtId);

            // 2. Display Name
            Label lblName = new Label { Text = "显示名称 (UI 显示标题，如 DeepSeek V3):", AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            mainLayout.Controls.Add(lblName);

            txtName = new TextBox { Text = isEdit && !string.IsNullOrEmpty(initial.displayName) ? initial.displayName : "", Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 12) };
            mainLayout.Controls.Add(txtName);

            // 3. Token Limits Group
            GroupBox grpParams = new GroupBox
            {
                Text = "Token 上下文与输出限制",
                Dock = DockStyle.Top,
                Height = 135,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(12, 10, 12, 10)
            };

            TableLayoutPanel paramsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3
            };
            paramsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            paramsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));

            Label lblIn = new Label { Text = "上下文窗口 (maxInputTokens):", Anchor = AnchorStyles.Left, AutoSize = true };
            paramsLayout.Controls.Add(lblIn, 0, 0);

            numIn = new NumericUpDown
            {
                Minimum = 1024,
                Maximum = 10000000,
                Increment = 4096,
                Value = isEdit && initial.maxInputTokens > 0 ? initial.maxInputTokens : 131072,
                Dock = DockStyle.Fill
            };
            paramsLayout.Controls.Add(numIn, 1, 0);

            Label lblOut = new Label { Text = "最大输出 (maxOutputTokens):", Anchor = AnchorStyles.Left, AutoSize = true };
            paramsLayout.Controls.Add(lblOut, 0, 1);

            numOut = new NumericUpDown
            {
                Minimum = 512,
                Maximum = 10000000,
                Increment = 1024,
                Value = isEdit && initial.maxOutputTokens > 0 ? initial.maxOutputTokens : 32768,
                Dock = DockStyle.Fill
            };
            paramsLayout.Controls.Add(numOut, 1, 1);

            Label lblField = new Label { Text = "Token 参数字段名称:", Anchor = AnchorStyles.Left, AutoSize = true };
            paramsLayout.Controls.Add(lblField, 0, 2);

            cmbField = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            cmbField.Items.Add("max_tokens");
            cmbField.Items.Add("max_completion_tokens");
            cmbField.SelectedItem = isEdit && initial.maxTokensField == "max_completion_tokens" ? "max_completion_tokens" : "max_tokens";
            paramsLayout.Controls.Add(cmbField, 1, 2);

            grpParams.Controls.Add(paramsLayout);
            mainLayout.Controls.Add(grpParams);

            // 4. Capabilities Group
            GroupBox grpCaps = new GroupBox
            {
                Text = "模型能力特性 (Capabilities)",
                Dock = DockStyle.Top,
                Height = 68,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(12, 8, 12, 8)
            };

            FlowLayoutPanel capsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false
            };

            chkTools = new CheckBox { Text = "工具调用 (tools)", Checked = isEdit ? initial.tools : true, AutoSize = true, Margin = new Padding(0, 4, 20, 0) };
            chkReason = new CheckBox { Text = "深度思考 (reasoning)", Checked = isEdit ? initial.reasoning : true, AutoSize = true, Margin = new Padding(0, 4, 20, 0) };
            chkVision = new CheckBox { Text = "视觉多模态 (vision)", Checked = isEdit ? initial.vision : false, AutoSize = true, Margin = new Padding(0, 4, 0, 0) };

            capsFlow.Controls.Add(chkTools);
            capsFlow.Controls.Add(chkReason);
            capsFlow.Controls.Add(chkVision);

            grpCaps.Controls.Add(capsFlow);
            mainLayout.Controls.Add(grpCaps);

            // 5. Action Buttons
            FlowLayoutPanel btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 48,
                Padding = new Padding(0, 8, 0, 0),
                BackColor = Color.Transparent
            };

            Button btnCancel = new Button
            {
                Text = "取消 (Cancel)",
                Size = new Size(95, 32),
                DialogResult = DialogResult.Cancel,
                Margin = new Padding(8, 0, 0, 0)
            };

            Button btnOk = new Button
            {
                Text = isEdit ? "保存更新" : "添加模型",
                Size = new Size(110, 32),
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(235, 245, 255),
                FlatStyle = FlatStyle.System,
                Margin = new Padding(8, 0, 0, 0)
            };
            btnOk.Click += (s, e) =>
            {
                string idVal = txtId.Text.Trim();
                if (string.IsNullOrEmpty(idVal))
                {
                    MessageBox.Show(this, "Model ID 不能为空。", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.None;
                    return;
                }
                string nameVal = txtName.Text.Trim();
                ResultModel = new ModelItem
                {
                    id = idVal,
                    displayName = string.IsNullOrEmpty(nameVal) ? idVal : nameVal,
                    vision = chkVision.Checked,
                    reasoning = chkReason.Checked,
                    tools = chkTools.Checked,
                    maxInputTokens = (int)numIn.Value,
                    maxOutputTokens = (int)numOut.Value,
                    maxTokensField = cmbField.SelectedItem.ToString(),
                    efforts = initial != null ? initial.efforts : new string[0],
                    supportsDisabled = initial != null ? initial.supportsDisabled : null,
                    providerId = initial != null ? initial.providerId : "",
                    providerName = initial != null ? initial.providerName : "",
                    upstreamBaseUrl = initial != null ? initial.upstreamBaseUrl : "",
                    uiBaseUrl = initial != null ? initial.uiBaseUrl : "",
                    selectedForInjection = initial != null ? initial.selectedForInjection : true
                };
            };

            btnPanel.Controls.Add(btnCancel);
            btnPanel.Controls.Add(btnOk);
            mainLayout.Controls.Add(btnPanel);

            Controls.Add(mainLayout);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
