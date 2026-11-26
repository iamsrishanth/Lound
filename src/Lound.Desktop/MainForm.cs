using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lound.Desktop
{
    internal sealed partial class MainForm : Form
    {
        private readonly ClientSession _session;
        private readonly ApiClient _api;
        private readonly TableLayoutPanel _shell = new TableLayoutPanel();
        private readonly TableLayoutPanel _right = new TableLayoutPanel();
        private readonly Panel _content = new Panel();
        private readonly FlowLayoutPanel _navigation = new FlowLayoutPanel();
        private readonly Label _pageTitle = new Label();
        private readonly Label _accountLabel = new Label();
        private readonly List<Button> _navButtons = new List<Button>();
        private Button _activeNav;

        public bool SignedOut { get; private set; }

        public MainForm(ClientSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _api = session.Api;
            Text = "Lound | Lost and Found Management";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1060, 700);
            Size = new Size(1280, 820);
            BackColor = Theme.Canvas;
            Font = Theme.Body();
            AutoScaleMode = AutoScaleMode.Font;
            KeyPreview = true;

            BuildShell();
            AddNavigation("Search items", ShowSearch);
            AddNavigation("Report an item", ShowReportForm);
            AddNavigation("My reports", ShowMyReports);
            AddNavigation("My claims", ShowMyClaims);
            AddNavigation("Profile", ShowProfile);
            if (_session.User != null && _session.User.IsAdmin)
            {
                AddNavigation("Admin dashboard", ShowAdminDashboard, true);
                AddNavigation("Manage users", ShowAdminUsers);
                AddNavigation("Item reports", ShowAdminItems);
                AddNavigation("Review claims", ShowAdminClaims);
                AddNavigation("Statistics", ShowAdminStatistics);
            }

            Shown += (s, e) => Navigate("Search items", ShowSearch);
            FormClosed += (s, e) => _api.Dispose();
        }

        private void BuildShell()
        {
            _shell.Dock = DockStyle.Fill;
            _shell.BackColor = Theme.Canvas;
            _shell.ColumnCount = 2;
            _shell.RowCount = 1;
            _shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 232));
            _shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(_shell);

            var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Navy, Padding = new Padding(0) };
            _shell.Controls.Add(sidebar, 0, 0);
            var sidebarLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Theme.Navy
            };
            sidebarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            sidebar.Controls.Add(sidebarLayout);

            var brand = new Panel { Dock = DockStyle.Top, Height = 108, BackColor = Theme.Navy };
            sidebarLayout.Controls.Add(brand, 0, 0);
            brand.Controls.Add(new Label
            {
                Text = "LOUND",
                AutoSize = true,
                Font = Theme.Body(22f, FontStyle.Bold),
                ForeColor = Theme.Surface,
                Location = new Point(24, 20)
            });
            brand.Controls.Add(new Label
            {
                Text = "LOST AND FOUND",
                AutoSize = true,
                Font = Theme.Body(8.2f, FontStyle.Bold),
                ForeColor = Color.FromArgb(205, 216, 230),
                Location = new Point(26, 58)
            });
            brand.Controls.Add(new Panel { BackColor = Theme.Gold, Location = new Point(25, 82), Size = new Size(44, 4) });

            var navHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Navy, Padding = new Padding(12, 10, 12, 8) };
            sidebarLayout.Controls.Add(navHost, 0, 1);
            _navigation.Dock = DockStyle.Fill;
            _navigation.FlowDirection = FlowDirection.TopDown;
            _navigation.WrapContents = false;
            _navigation.AutoScroll = true;
            _navigation.BackColor = Theme.Navy;
            _navigation.Padding = new Padding(0);
            navHost.Controls.Add(_navigation);

            var footer = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Navy, Padding = new Padding(12, 10, 12, 12) };
            sidebarLayout.Controls.Add(footer, 0, 2);
            var signOut = new Button { Text = "Sign out", Dock = DockStyle.Fill, TabStop = true };
            signOut.FlatStyle = FlatStyle.Flat;
            signOut.FlatAppearance.BorderColor = Color.FromArgb(84, 107, 137);
            signOut.FlatAppearance.BorderSize = 1;
            signOut.BackColor = Theme.Navy;
            signOut.ForeColor = Theme.Surface;
            signOut.Font = Theme.Body(9.5f, FontStyle.Bold);
            signOut.Cursor = Cursors.Hand;
            signOut.FlatAppearance.MouseOverBackColor = Theme.NavyLight;
            signOut.FlatAppearance.MouseDownBackColor = Color.FromArgb(47, 70, 96);
            signOut.Click += async (s, e) => await SignOutAsync();
            footer.Controls.Add(signOut);

            _right.Dock = DockStyle.Fill;
            _right.ColumnCount = 1;
            _right.RowCount = 2;
            _right.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            _right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _right.BackColor = Theme.Canvas;
            _shell.Controls.Add(_right, 1, 0);

            var header = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(24, 12, 24, 10) };
            header.Controls.Add(_pageTitle);
            _pageTitle.AutoSize = true;
            _pageTitle.Font = Theme.Body(17f, FontStyle.Bold);
            _pageTitle.ForeColor = Theme.Navy;
            _pageTitle.Location = new Point(24, 27);
            _accountLabel.AutoSize = true;
            _accountLabel.TextAlign = ContentAlignment.MiddleRight;
            _accountLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _accountLabel.Font = Theme.Body(9.5f, FontStyle.Bold);
            _accountLabel.ForeColor = Theme.Muted;
            _accountLabel.Text = _session.User == null ? string.Empty : _session.User.DisplayName;
            header.Controls.Add(_accountLabel);
            header.Resize += (s, e) => _accountLabel.Location = new Point(Math.Max(280, header.ClientSize.Width - _accountLabel.Width - 28), 31);
            _accountLabel.Location = new Point(Math.Max(280, header.ClientSize.Width - _accountLabel.Width - 28), 31);
            header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });
            _right.Controls.Add(header, 0, 0);

            _content.Dock = DockStyle.Fill;
            _content.BackColor = Theme.Canvas;
            _content.Padding = new Padding(24, 20, 24, 22);
            _right.Controls.Add(_content, 0, 1);
        }

        private void AddNavigation(string text, Action action, bool sectionStart = false)
        {
            if (sectionStart)
            {
                var divider = new Label
                {
                    Text = "ADMINISTRATION",
                    AutoSize = false,
                    Width = 202,
                    Height = 30,
                    TextAlign = ContentAlignment.BottomLeft,
                    Font = Theme.Body(8f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(169, 188, 209),
                    Margin = new Padding(3, 18, 0, 5)
                };
                _navigation.Controls.Add(divider);
            }

            var button = new Button
            {
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                Width = 202,
                Height = 44,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Navy,
                ForeColor = Color.FromArgb(231, 237, 244),
                Font = Theme.Body(9.5f, FontStyle.Bold),
                Padding = new Padding(16, 0, 8, 0),
                Margin = new Padding(0, 2, 0, 2),
                Cursor = Cursors.Hand,
                TabStop = true
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Theme.NavyLight;
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(47, 70, 96);
            button.Click += (s, e) => Navigate(text, action, button);
            _navigation.Controls.Add(button);
            _navButtons.Add(button);
        }

        private void Navigate(string title, Action action, Button button = null)
        {
            if (button != null)
            {
                if (_activeNav != null)
                {
                    _activeNav.BackColor = Theme.Navy;
                    _activeNav.ForeColor = Color.FromArgb(231, 237, 244);
                }
                _activeNav = button;
                _activeNav.BackColor = Theme.NavyLight;
                _activeNav.ForeColor = Theme.Gold;
            }
            _pageTitle.Text = title;
            action();
        }

        private Panel NewPage()
        {
            _content.Controls.Clear();
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas };
            _content.Controls.Add(panel);
            return panel;
        }

        private Label NewStatus(Panel parent, string text = "")
        {
            var label = new Label
            {
                Text = text,
                Dock = DockStyle.Bottom,
                AutoSize = false,
                Height = string.IsNullOrWhiteSpace(text) ? 1 : 34,
                ForeColor = Theme.Muted,
                Font = Theme.Body(9f),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(1, 3, 1, 3)
            };
            parent.Controls.Add(label);
            label.BringToFront();
            return label;
        }

        private static Label PageHint(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 36,
                ForeColor = Theme.Muted,
                Font = Theme.Body(9.5f),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 2, 0)
            };
        }

        private void ShowError(Label label, Exception ex)
        {
            label.ForeColor = Theme.Danger;
            label.Text = ex is ApiException ? ex.Message : "The request could not be completed. Check the API connection and try again.";
            label.Height = 42;
        }

        private static string Q(string value) => Uri.EscapeDataString(value ?? string.Empty);

        private async Task<IReadOnlyList<T>> SafeListAsync<T>(string path)
        {
            return await _api.GetListAsync<T>(path);
        }

        private async Task SignOutAsync()
        {
            try { await _api.LogoutAsync(); }
            catch { _api.SetAccessToken(null); }
            SignedOut = true;
            Close();
        }

        private void ShowSearch()
        {
            var page = NewPage();
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Theme.Canvas
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.Controls.Add(PageHint("Search verified reports. Open a result for details and claim instructions."), 0, 0);
            page.Controls.Add(layout);

            var filterPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(12, 8, 12, 8), BorderStyle = BorderStyle.FixedSingle };
            var filters = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, BackColor = Theme.Surface };
            for (var i = 0; i < 4; i++) filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            filters.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            filters.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            filterPanel.Controls.Add(filters);

            var keyword = new TextBox();
            var category = new TextBox();
            var location = new TextBox();
            var from = new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
            var to = new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
            var type = MakeCombo("Any type", "Lost", "Found");
            var itemStatus = MakeCombo("Any status", "Lost", "Found", "Claimed", "Returned");
            AddFilter(filters, "Keyword", keyword, 0, 0);
            AddFilter(filters, "Category", category, 1, 0);
            AddFilter(filters, "Location", location, 2, 0);
            AddFilter(filters, "Report type", type, 3, 0);
            AddFilter(filters, "From date", from, 0, 1);
            AddFilter(filters, "To date", to, 1, 1);
            AddFilter(filters, "Item status", itemStatus, 2, 1);
            var find = new Button { Text = "Search", Anchor = AnchorStyles.Left | AnchorStyles.Bottom };
            Theme.StyleButton(find, true);
            find.Width = 132;
            find.Margin = new Padding(8, 15, 4, 3);
            filters.Controls.Add(find, 3, 1);
            layout.Controls.Add(filterPanel, 0, 1);

            var gridHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(1), BorderStyle = BorderStyle.FixedSingle };
            var grid = Theme.Grid();
            AddGridColumn(grid, "Title", nameof(ItemDto.Title), 1.4f);
            AddGridColumn(grid, "Category", nameof(ItemDto.Category));
            AddGridColumn(grid, "Location", nameof(ItemDto.Location), 1.1f);
            AddGridColumn(grid, "Date", nameof(ItemDto.IncidentDate));
            AddGridColumn(grid, "Type", nameof(ItemDto.ReportType));
            AddGridColumn(grid, "Status", nameof(ItemDto.ItemStatus));
            gridHost.Controls.Add(grid);
            layout.Controls.Add(gridHost, 0, 2);
            var status = new Label { Dock = DockStyle.Fill, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft, Font = Theme.Body(9f) };
            layout.Controls.Add(status, 0, 3);

            find.Click += async (s, e) => await RunSearchAsync();
            grid.CellDoubleClick += async (s, e) =>
            {
                if (e.RowIndex >= 0 && grid.CurrentRow?.DataBoundItem is ItemDto selected)
                    await OpenItemDetailsAsync(selected.Id);
            };
            void SetRows(IReadOnlyList<ItemDto> rows)
            {
                grid.DataSource = rows.ToList();
                status.ForeColor = rows.Count == 0 ? Theme.Muted : Theme.Success;
                status.Text = rows.Count == 0 ? "No verified items match these filters. Try a broader search." : rows.Count + " verified item(s) found. Double-click a row to view details.";
            }
            async Task RunSearchAsync()
            {
                status.ForeColor = Theme.Muted;
                status.Text = "Loading verified reports...";
                find.Enabled = false;
                try
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(keyword.Text)) parts.Add("keyword=" + Q(keyword.Text.Trim()));
                    if (!string.IsNullOrWhiteSpace(category.Text)) parts.Add("category=" + Q(category.Text.Trim()));
                    if (!string.IsNullOrWhiteSpace(location.Text)) parts.Add("location=" + Q(location.Text.Trim()));
                    if (from.Checked) parts.Add("from=" + Q(from.Value.ToString("yyyy-MM-dd")));
                    if (to.Checked) parts.Add("to=" + Q(to.Value.ToString("yyyy-MM-dd")));
                    if (from.Checked && to.Checked && from.Value.Date > to.Value.Date)
                        throw new InvalidOperationException("From date must be on or before the to date.");
                    if (type.SelectedIndex > 0) parts.Add("reportType=" + Q(type.Text));
                    if (itemStatus.SelectedIndex > 0) parts.Add("itemStatus=" + Q(itemStatus.Text));
                    parts.Add("page=1");
                    parts.Add("pageSize=100");
                    var path = "v1/items" + (parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts));
                    SetRows(await SafeListAsync<ItemDto>(path));
                }
                catch (Exception ex)
                {
                    status.ForeColor = Theme.Danger;
                    status.Text = ex.Message;
                }
                finally { find.Enabled = true; }
            }
            _ = RunSearchAsync();
        }

        private void ShowReportForm()
        {
            var page = NewPage();
            var form = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                ColumnCount = 4,
                RowCount = 5,
                BackColor = Theme.Surface,
                Padding = new Padding(18),
                Margin = new Padding(0),
                BorderStyle = BorderStyle.FixedSingle
            };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (var i = 0; i < 5; i++) form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var title = new TextBox();
            var category = new TextBox();
            var location = new TextBox();
            var date = new DateTimePicker { Format = DateTimePickerFormat.Short, MaxDate = DateTime.Today };
            var reportType = MakeCombo("Lost", "Found");
            var description = new TextBox { Multiline = true, Height = 86, ScrollBars = ScrollBars.Vertical };
            AddFormField(form, 0, "Title", title, 0, 1);
            AddFormField(form, 0, "Category", category, 2, 3);
            AddFormField(form, 1, "Location", location, 0, 1);
            AddFormField(form, 1, "Incident date", date, 2, 3);
            AddFormField(form, 2, "Report type", reportType, 0, 1);
            AddFormField(form, 3, "Description", description, 0, 3);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Theme.Canvas };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            root.Controls.Add(PageHint("Add the details needed to identify and verify the item. New reports start pending review."), 0, 0);
            root.Controls.Add(form, 0, 1);

            var actionRow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0), WrapContents = false };
            var submit = new Button { Text = "Submit report" };
            Theme.StyleButton(submit, true);
            var status = new Label { AutoSize = false, Width = 620, Height = 38, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Muted, Padding = new Padding(8, 0, 0, 0) };
            actionRow.Controls.Add(submit);
            actionRow.Controls.Add(status);
            root.Controls.Add(actionRow, 0, 2);
            page.Controls.Add(root);

            submit.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(title.Text) || string.IsNullOrWhiteSpace(category.Text) || string.IsNullOrWhiteSpace(location.Text) || string.IsNullOrWhiteSpace(description.Text))
                {
                    status.ForeColor = Theme.Danger;
                    status.Text = "Complete every field before submitting.";
                    return;
                }
                submit.Enabled = false;
                submit.Text = "Submitting...";
                status.Text = "Sending report for review...";
                try
                {
                    await _api.PostAsync<ItemDto>("v1/items", new
                    {
                        title = title.Text.Trim(), category = category.Text.Trim(), description = description.Text.Trim(),
                        location = location.Text.Trim(), incidentDate = date.Value.ToString("yyyy-MM-dd"), reportType = reportType.Text
                    });
                    status.ForeColor = Theme.Success;
                    status.Text = "Report submitted. You can track its moderation status under My reports.";
                    title.Clear(); category.Clear(); location.Clear(); description.Clear();
                    reportType.SelectedIndex = 0;
                }
                catch (Exception ex) { status.ForeColor = Theme.Danger; status.Text = ex.Message; }
                finally { submit.Enabled = true; submit.Text = "Submit report"; }
            };
        }

        private async void ShowMyReports()
        {
            var page = NewPage();
            var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Theme.Canvas };
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            workspace.Controls.Add(PageHint("Your submitted reports, including pending moderation and item status."), 0, 0);
            var grid = Theme.Grid();
            AddGridColumn(grid, "Title", nameof(ItemDto.Title), 1.5f);
            AddGridColumn(grid, "Category", nameof(ItemDto.Category));
            AddGridColumn(grid, "Date", nameof(ItemDto.IncidentDate));
            AddGridColumn(grid, "Type", nameof(ItemDto.ReportType));
            AddGridColumn(grid, "Moderation", nameof(ItemDto.ModerationStatus));
            AddGridColumn(grid, "Item status", nameof(ItemDto.ItemStatus));
            workspace.Controls.Add(grid, 0, 1);
            var status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Muted, Font = Theme.Body(9f), Text = "Loading your reports..." };
            workspace.Controls.Add(status, 0, 2);
            page.Controls.Add(workspace);
            try
            {
                var rows = await SafeListAsync<ItemDto>("v1/me/items?page=1&pageSize=100");
                grid.DataSource = rows.ToList();
                status.ForeColor = Theme.Muted;
                status.Text = rows.Count == 0 ? "You have not submitted any reports yet. Use Report an item to get started." : rows.Count + " report(s).";
            }
            catch (Exception ex) { ShowError(status, ex); }
        }

        private async void ShowMyClaims()
        {
            var page = NewPage();
            var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Theme.Canvas };
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            workspace.Controls.Add(PageHint("Track your claims and their outcomes. Your ownership evidence is visible only to you and administrators."), 0, 0);
            var grid = Theme.Grid();
            AddGridColumn(grid, "Item", nameof(ClaimDto.ItemTitle), 1.4f);
            AddGridColumn(grid, "Status", nameof(ClaimDto.Status));
            AddGridColumn(grid, "Submitted", nameof(ClaimDto.SubmittedAt), 1.2f);
            AddGridColumn(grid, "Returned", nameof(ClaimDto.ReturnedAt), 1.2f);
            workspace.Controls.Add(grid, 0, 1);
            var status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Muted, Font = Theme.Body(9f), Text = "Loading your claims..." };
            workspace.Controls.Add(status, 0, 2);
            page.Controls.Add(workspace);
            grid.CellDoubleClick += (s, e) =>
            {
                if (grid.CurrentRow?.DataBoundItem is ClaimDto claim)
                    ShowClaimDetails(claim, false);
            };
            try
            {
                var rows = await SafeListAsync<ClaimDto>("v1/me/claims?page=1&pageSize=100");
                grid.DataSource = rows.ToList();
                status.ForeColor = Theme.Muted;
                status.Text = rows.Count == 0 ? "You have not submitted any claims." : rows.Count + " claim(s). Double-click a row to review your evidence and status.";
            }
            catch (Exception ex) { ShowError(status, ex); }
        }

        private void ShowProfile()
        {
            var page = NewPage();
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Theme.Canvas };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(PageHint("Update the name shown on your account."), 0, 0);
            var form = new Panel { BackColor = Theme.Surface, Dock = DockStyle.Top, Height = 230, Padding = new Padding(22), BorderStyle = BorderStyle.FixedSingle };
            var name = new TextBox { Text = _session.User?.DisplayName ?? string.Empty, Width = 400 };
            Theme.StyleInput(name);
            var email = new TextBox { Text = _session.User?.Email ?? string.Empty, Width = 400, ReadOnly = true };
            Theme.StyleInput(email);
            var save = new Button { Text = "Save profile" };
            Theme.StyleButton(save, true);
            var status = new Label { AutoSize = false, Height = 40, Width = 550, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft };
            form.Controls.AddRange(new Control[]
            {
                Theme.FieldLabel("Display name"), name,
                Theme.FieldLabel("Email"), email,
                save, status
            });
            form.Controls[0].Location = new Point(22, 18);
            name.Location = new Point(22, 40);
            form.Controls[2].Location = new Point(22, 82);
            email.Location = new Point(22, 104);
            save.Location = new Point(22, 152);
            status.Location = new Point(136, 150);
            root.Controls.Add(form, 0, 1);
            page.Controls.Add(root);
            save.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(name.Text))
                {
                    status.ForeColor = Theme.Danger;
                    status.Text = "Display name is required.";
                    return;
                }
                save.Enabled = false;
                try
                {
                    var user = await _api.PatchAsync<UserDto>("v1/auth/me", new { displayName = name.Text.Trim() });
                    _session.User.DisplayName = user?.DisplayName ?? name.Text.Trim();
                    _accountLabel.Text = _session.User.DisplayName;
                    status.ForeColor = Theme.Success;
                    status.Text = "Profile saved.";
                }
                catch (Exception ex) { status.ForeColor = Theme.Danger; status.Text = ex.Message; }
                finally { save.Enabled = true; }
            };
        }

        private async Task OpenItemDetailsAsync(string id)
        {
            try
            {
                var item = await _api.GetAsync<ItemDto>("v1/items/" + Uri.EscapeDataString(id));
                if (item == null) throw new InvalidOperationException("The item is no longer available.");
                using (var details = new ItemDetailsForm(item, _session.User != null && _session.User.IsAdmin))
                {
                    if (details.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(details.Evidence))
                    {
                        await _api.PostAsync<ClaimDto>("v1/items/" + Uri.EscapeDataString(item.Id) + "/claims", new { evidence = details.Evidence });
                        MessageBox.Show(this, "Claim submitted. Track its status under My claims.", "Claim submitted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Unable to open item", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void ShowClaimDetails(ClaimDto claim, bool administrator)
        {
            var text = new StringBuilder();
            text.AppendLine("Item: " + (claim.ItemTitle ?? "Unknown item"));
            if (administrator && !string.IsNullOrWhiteSpace(claim.ClaimantDisplayName))
                text.AppendLine("Claimant: " + claim.ClaimantDisplayName);
            text.AppendLine("Status: " + claim.Status);
            text.AppendLine("Submitted: " + claim.SubmittedAt);
            if (!string.IsNullOrWhiteSpace(claim.ReturnedAt)) text.AppendLine("Returned: " + claim.ReturnedAt);
            text.AppendLine();
            text.AppendLine("Private ownership evidence:");
            text.AppendLine(string.IsNullOrWhiteSpace(claim.Evidence) ? "No evidence was included in this response." : claim.Evidence);
            MessageBox.Show(text.ToString(), "Claim details", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static ComboBox MakeCombo(params string[] options)
        {
            var combo = new ComboBox();
            combo.Items.AddRange(options);
            combo.SelectedIndex = 0;
            combo.Width = 160;
            Theme.StyleInput(combo);
            return combo;
        }

        private static void AddGridColumn(DataGridView grid, string header, string property, float fill = 1f)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                DataPropertyName = property,
                Name = property,
                FillWeight = fill,
                MinimumWidth = 78,
                SortMode = DataGridViewColumnSortMode.Automatic
            });
        }

        private static void AddFilter(TableLayoutPanel table, string label, Control input, int column, int row)
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5, 1, 5, 1), Margin = new Padding(2, 1, 2, 1) };
            var title = Theme.FieldLabel(label);
            title.SetBounds(4, 0, 170, 18);
            input.SetBounds(4, 20, Math.Max(112, table.Width / Math.Max(1, table.ColumnCount) - 16), 27);
            input.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            panel.Resize += (s, e) => input.Width = Math.Max(112, panel.ClientSize.Width - 8);
            if (input is TextBox) Theme.StyleInput(input);
            panel.Controls.Add(title);
            panel.Controls.Add(input);
            table.Controls.Add(panel, column, row);
        }

        private static void AddFormField(TableLayoutPanel table, int row, string label, Control input, int labelColumn, int inputColumn)
        {
            var fieldLabel = Theme.FieldLabel(label);
            fieldLabel.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            table.Controls.Add(fieldLabel, labelColumn, row);
            input.Dock = DockStyle.Top;
            input.Margin = new Padding(0, 4, 14, 8);
            if (input is TextBox || input is ComboBox) Theme.StyleInput(input);
            table.Controls.Add(input, inputColumn, row);
        }
    }
}
