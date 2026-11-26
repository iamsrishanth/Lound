using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lound.Desktop
{
    internal sealed partial class MainForm
    {
        private async void ShowAdminDashboard()
        {
            var page = NewPage();
            var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Theme.Canvas };
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            workspace.Controls.Add(PageHint("Live system counts from the API database."), 0, 0);
            var load = new Button { Text = "Refresh" };
            Theme.StyleButton(load);
            var loadRow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 4) };
            loadRow.Controls.Add(load);
            workspace.Controls.Add(loadRow, 0, 1);
            var metrics = new (string Key, string Caption)[]
            {
                ("usersTotal", "Total users"), ("activeUsers", "Active users"), ("reportsTotal", "Total reports"), ("pendingReports", "Pending reports"),
                ("verifiedReports", "Verified reports"), ("rejectedReports", "Rejected reports"), ("removedReports", "Removed reports"), ("lostItems", "Lost items"),
                ("foundItems", "Found items"), ("claimedItems", "Claimed items"), ("returnedItems", "Returned items"), ("claimsTotal", "Total claims"),
                ("pendingClaims", "Pending claims"), ("approvedClaims", "Approved claims"), ("rejectedClaims", "Rejected claims"), ("returnedClaims", "Returned claims")
            };
            var values = new Dictionary<string, Label>();
            var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 4, BackColor = Theme.Canvas, Padding = new Padding(0) };
            for (var column = 0; column < 4; column++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            for (var row = 0; row < 4; row++) cards.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            for (var index = 0; index < metrics.Length; index++)
            {
                var info = metrics[index];
                var card = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(7), Padding = new Padding(14) };
                var caption = new Label { Text = info.Caption, AutoSize = true, Font = Theme.Body(9.5f, FontStyle.Bold), ForeColor = Theme.Muted, Location = new Point(14, 13) };
                var count = new Label { Text = "Loading", AutoSize = true, Font = Theme.Body(24f, FontStyle.Bold), ForeColor = Theme.Navy, Location = new Point(14, 40), AccessibleName = info.Caption + " count" };
                card.Controls.Add(caption);
                card.Controls.Add(count);
                cards.Controls.Add(card, index % 4, index / 4);
                values[info.Key] = count;
            }
            workspace.Controls.Add(cards, 0, 2);
            var status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Muted, Font = Theme.Body(9f), Text = "Loading live counts..." };
            workspace.Controls.Add(status, 0, 3);
            page.Controls.Add(workspace);

            async Task RefreshAsync()
            {
                load.Enabled = false;
                status.Text = "Loading live counts...";
                status.ForeColor = Theme.Muted;
                try
                {
                    var data = await _api.GetAsync<DashboardDto>("v1/admin/dashboard");
                    if (data == null) throw new InvalidOperationException("No dashboard data was returned.");
                    var numbers = new Dictionary<string, int>
                    {
                        ["usersTotal"] = data.UsersTotal, ["activeUsers"] = data.ActiveUsers, ["reportsTotal"] = data.ReportsTotal,
                        ["pendingReports"] = data.PendingReports, ["verifiedReports"] = data.VerifiedReports, ["rejectedReports"] = data.RejectedReports,
                        ["removedReports"] = data.RemovedReports, ["lostItems"] = data.LostItems, ["foundItems"] = data.FoundItems,
                        ["claimedItems"] = data.ClaimedItems, ["returnedItems"] = data.ReturnedItems, ["claimsTotal"] = data.ClaimsTotal,
                        ["pendingClaims"] = data.PendingClaims, ["approvedClaims"] = data.ApprovedClaims, ["rejectedClaims"] = data.RejectedClaims,
                        ["returnedClaims"] = data.ReturnedClaims
                    };
                    foreach (var pair in numbers) values[pair.Key].Text = pair.Value.ToString("N0");
                    status.Text = "Dashboard refreshed from the live database.";
                    status.ForeColor = Theme.Success;
                }
                catch (Exception ex) { ShowError(status, ex); }
                finally { load.Enabled = true; }
            }
            load.Click += async (s, e) => await RefreshAsync();
            _ = RefreshAsync();
        }

        private async void ShowAdminUsers()
        {
            var page = NewPage();
            var hint = PageHint("Manage account access while preserving report and claim history.");
            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Theme.Canvas, Height = 104 };
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            var name = new TextBox { Width = 160 };
            var email = new TextBox { Width = 190 };
            var password = new TextBox { Width = 160, UseSystemPasswordChar = true };
            Theme.StyleInput(name); Theme.StyleInput(email); Theme.StyleInput(password);
            var add = new Button { Text = "Add user" };
            Theme.StyleButton(add, true);
            var deactivate = new Button { Text = "Deactivate" };
            var reactivate = new Button { Text = "Reactivate" };
            var remove = new Button { Text = "Remove from directory" };
            var restore = new Button { Text = "Restore to directory" };
            Theme.StyleButton(deactivate); Theme.StyleButton(reactivate); Theme.StyleButton(remove, false, true); Theme.StyleButton(restore);
            var createRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 2, 0, 2) };
            createRow.Controls.AddRange(new Control[]
            {
                LabeledInput("Display name", name, 178), LabeledInput("Email", email, 205),
                LabeledInput("Initial password", password, 178), add
            });
            var stateRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 2, 0, 2) };
            stateRow.Controls.AddRange(new Control[] { deactivate, reactivate, remove, restore });
            actions.Controls.Add(createRow, 0, 0);
            actions.Controls.Add(stateRow, 0, 1);
            var (grid, status) = AddAdminGridPage(page, hint, actions);
            AddGridColumn(grid, "Display name", nameof(UserDto.DisplayName), 1.2f);
            AddGridColumn(grid, "Email", nameof(UserDto.Email), 1.5f);
            AddGridColumn(grid, "Active", nameof(UserDto.IsActive));
            AddGridColumn(grid, "In directory", nameof(UserDto.IsDirectoryVisible));
            AddGridColumn(grid, "Created", nameof(UserDto.CreatedAt), 1.2f);

            async Task RefreshAsync()
            {
                status.Text = "Loading users...";
                try
                {
                    var rows = await SafeListAsync<UserDto>("v1/admin/users?includeInactive=true&page=1&pageSize=100");
                    grid.DataSource = rows.ToList();
                    status.ForeColor = Theme.Muted;
                    status.Text = rows.Count == 0 ? "No accounts found." : rows.Count + " account(s). Select a row to manage access.";
                }
                catch (Exception ex) { ShowError(status, ex); }
            }
            UserDto Selected() => grid.CurrentRow?.DataBoundItem as UserDto;
            async Task RunUserActionAsync(Button button, string action)
            {
                var user = Selected();
                if (user == null) { status.ForeColor = Theme.Danger; status.Text = "Select a user first."; return; }
                button.Enabled = false;
                try
                {
                    await _api.PostNoContentAsync("v1/admin/users/" + Uri.EscapeDataString(user.Id) + "/" + action);
                    await RefreshAsync();
                }
                catch (Exception ex) { ShowError(status, ex); }
                finally { button.Enabled = true; }
            }
            add.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(email.Text) || string.IsNullOrWhiteSpace(password.Text))
                {
                    status.ForeColor = Theme.Danger; status.Text = "Enter a name, email, and initial password."; return;
                }
                add.Enabled = false;
                try
                {
                    await _api.PostAsync<UserDto>("v1/admin/users", new { displayName = name.Text.Trim(), email = email.Text.Trim(), password = password.Text });
                    name.Clear(); email.Clear(); password.Clear();
                    await RefreshAsync();
                }
                catch (Exception ex) { ShowError(status, ex); }
                finally { add.Enabled = true; }
            };
            deactivate.Click += async (s, e) => await RunUserActionAsync(deactivate, "deactivate");
            reactivate.Click += async (s, e) => await RunUserActionAsync(reactivate, "reactivate");
            remove.Click += async (s, e) => await RunUserActionAsync(remove, "remove-from-directory");
            restore.Click += async (s, e) => await RunUserActionAsync(restore, "restore-to-directory");
            _ = RefreshAsync();
        }

        private async void ShowAdminItems()
        {
            var page = NewPage();
            var hint = PageHint("Review and maintain submitted reports. Verify reports before they appear in shared search.");
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 92, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(8, 5, 8, 5), BackColor = Theme.Surface, BorderStyle = BorderStyle.FixedSingle };
            var moderation = MakeCombo("Any moderation", "Pending", "Verified", "Rejected", "Removed");
            moderation.Width = 155;
            var itemStatus = MakeCombo("Any item status", "Lost", "Found", "Claimed", "Returned");
            itemStatus.Width = 150;
            var reload = new Button { Text = "Load reports" };
            var verify = new Button { Text = "Verify" };
            var reject = new Button { Text = "Reject" };
            var remove = new Button { Text = "Remove" };
            var found = new Button { Text = "Mark found" };
            var edit = new Button { Text = "Edit details" };
            Theme.StyleButton(reload, true); Theme.StyleButton(verify); Theme.StyleButton(reject, false, true); Theme.StyleButton(remove, false, true); Theme.StyleButton(found); Theme.StyleButton(edit);
            actions.Controls.AddRange(new Control[] { moderation, itemStatus, reload, verify, reject, remove, found, edit });
            var (grid, status) = AddAdminGridPage(page, hint, actions);
            AddGridColumn(grid, "Title", nameof(ItemDto.Title), 1.4f);
            AddGridColumn(grid, "Category", nameof(ItemDto.Category));
            AddGridColumn(grid, "Location", nameof(ItemDto.Location), 1.1f);
            AddGridColumn(grid, "Incident date", nameof(ItemDto.IncidentDate));
            AddGridColumn(grid, "Type", nameof(ItemDto.ReportType));
            AddGridColumn(grid, "Moderation", nameof(ItemDto.ModerationStatus));
            AddGridColumn(grid, "Item status", nameof(ItemDto.ItemStatus));

            async Task RefreshAsync()
            {
                status.Text = "Loading reports...";
                try
                {
                    var parts = new List<string> { "page=1", "pageSize=100" };
                    if (moderation.SelectedIndex > 0) parts.Add("moderationStatus=" + Q(moderation.Text));
                    if (itemStatus.SelectedIndex > 0) parts.Add("itemStatus=" + Q(itemStatus.Text));
                    var rows = await SafeListAsync<ItemDto>("v1/admin/items?" + string.Join("&", parts));
                    grid.DataSource = rows.ToList();
                    status.ForeColor = Theme.Muted;
                    status.Text = rows.Count == 0 ? "No reports match these filters." : rows.Count + " report(s). Select a row, then choose an action.";
                }
                catch (Exception ex) { ShowError(status, ex); }
            }
            ItemDto Selected() => grid.CurrentRow?.DataBoundItem as ItemDto;
            async Task RunItemActionAsync(Button button, string action, bool needsReason = false)
            {
                var item = Selected();
                if (item == null) { status.ForeColor = Theme.Danger; status.Text = "Select a report first."; return; }
                var reason = needsReason ? TextInputDialog.Prompt(this, action == "reject" ? "Reject report" : "Remove report", "Add a reason for this decision:", true) : null;
                if (needsReason && reason == null) return;
                button.Enabled = false;
                try
                {
                    await _api.PostNoContentAsync("v1/admin/items/" + Uri.EscapeDataString(item.Id) + "/" + action,
                        needsReason ? new { reason } : null);
                    await RefreshAsync();
                }
                catch (Exception ex) { ShowError(status, ex); }
                finally { button.Enabled = true; }
            }
            reload.Click += async (s, e) => await RefreshAsync();
            verify.Click += async (s, e) => await RunItemActionAsync(verify, "verify");
            reject.Click += async (s, e) => await RunItemActionAsync(reject, "reject", true);
            remove.Click += async (s, e) => await RunItemActionAsync(remove, "remove", true);
            found.Click += async (s, e) => await RunItemActionAsync(found, "mark-found");
            edit.Click += async (s, e) =>
            {
                var item = Selected();
                if (item == null) { status.ForeColor = Theme.Danger; status.Text = "Select a report first."; return; }
                using (var dialog = new EditItemDialog(item))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    edit.Enabled = false;
                    try
                    {
                        await _api.PatchAsync<ItemDto>("v1/admin/items/" + Uri.EscapeDataString(item.Id), dialog.Changes);
                        await RefreshAsync();
                    }
                    catch (Exception ex) { ShowError(status, ex); }
                    finally { edit.Enabled = true; }
                }
            };
            grid.CellDoubleClick += async (s, e) =>
            {
                if (grid.CurrentRow?.DataBoundItem is ItemDto item)
                    await OpenItemDetailsAsync(item.Id);
            };
            _ = RefreshAsync();
        }

        private async void ShowAdminClaims()
        {
            var page = NewPage();
            var hint = PageHint("Review private ownership evidence. Evidence is available only to the claimant and authorized administrators.");
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 92, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(8, 5, 8, 5), BackColor = Theme.Surface, BorderStyle = BorderStyle.FixedSingle };
            var filter = MakeCombo("Any status", "Pending", "Approved", "Rejected", "Returned");
            filter.Width = 145;
            var reload = new Button { Text = "Load claims" };
            var details = new Button { Text = "View evidence" };
            var approve = new Button { Text = "Approve claim" };
            var reject = new Button { Text = "Reject claim" };
            var returned = new Button { Text = "Record handover" };
            Theme.StyleButton(reload, true); Theme.StyleButton(details); Theme.StyleButton(approve); Theme.StyleButton(reject, false, true); Theme.StyleButton(returned);
            details.Enabled = false;
            approve.Enabled = false;
            reject.Enabled = false;
            returned.Enabled = false;
            actions.Controls.AddRange(new Control[] { filter, reload, details, approve, reject, returned });
            var (grid, status) = AddAdminGridPage(page, hint, actions);
            AddGridColumn(grid, "Item", nameof(ClaimDto.ItemTitle), 1.3f);
            AddGridColumn(grid, "Claimant", nameof(ClaimDto.ClaimantDisplayName));
            AddGridColumn(grid, "Status", nameof(ClaimDto.Status));
            AddGridColumn(grid, "Submitted", nameof(ClaimDto.SubmittedAt), 1.2f);
            AddGridColumn(grid, "Returned", nameof(ClaimDto.ReturnedAt), 1.2f);

            ClaimDto Selected() => grid.CurrentRow?.DataBoundItem as ClaimDto;
            void UpdateClaimActions()
            {
                var claim = Selected();
                var pending = claim != null && string.Equals(claim.Status, "Pending", StringComparison.OrdinalIgnoreCase);
                details.Enabled = claim != null;
                approve.Enabled = pending;
                reject.Enabled = pending;
                returned.Enabled = claim != null && string.Equals(claim.Status, "Approved", StringComparison.OrdinalIgnoreCase);
            }
            grid.SelectionChanged += (s, e) => UpdateClaimActions();

            async Task RefreshAsync(string preferredClaimId = null)
            {
                status.Text = "Loading claims...";
                try
                {
                    var path = "v1/admin/claims?page=1&pageSize=100" + (filter.SelectedIndex > 0 ? "&status=" + Q(filter.Text) : string.Empty);
                    var rows = await SafeListAsync<ClaimDto>(path);
                    grid.DataSource = rows.ToList();
                    grid.ClearSelection();
                    grid.CurrentCell = null;
                    if (!string.IsNullOrWhiteSpace(preferredClaimId))
                    {
                        var preferredRow = grid.Rows.Cast<DataGridViewRow>()
                            .FirstOrDefault(row => row.DataBoundItem is ClaimDto claim && claim.Id == preferredClaimId);
                        if (preferredRow != null && preferredRow.Cells.Count > 0)
                        {
                            preferredRow.Selected = true;
                            grid.CurrentCell = preferredRow.Cells[0];
                        }
                    }
                    UpdateClaimActions();
                    status.ForeColor = Theme.Muted;
                    status.Text = rows.Count == 0 ? "No claims match this filter." : rows.Count + " claim(s). Select a row to see the actions available for its status.";
                }
                catch (Exception ex) { ShowError(status, ex); }
            }
            async Task RunClaimActionAsync(Button button, string action, bool needsReason = false)
            {
                var claim = Selected();
                if (claim == null) { status.ForeColor = Theme.Danger; status.Text = "Select a claim first."; return; }
                if (action == "record-return" && !string.Equals(claim.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                {
                    status.ForeColor = Theme.Danger;
                    status.Text = "Record handover is available after a claim is approved.";
                    return;
                }
                var reason = needsReason ? TextInputDialog.Prompt(this, "Reject claim", "Add a reason for rejecting this claim:", true) : null;
                if (needsReason && reason == null) return;
                button.Enabled = false;
                try
                {
                    await _api.PostNoContentAsync("v1/admin/claims/" + Uri.EscapeDataString(claim.Id) + "/" + action,
                        needsReason ? new { reason } : null);
                    await RefreshAsync(claim.Id);
                }
                catch (Exception ex) { ShowError(status, ex); }
                finally { UpdateClaimActions(); }
            }
            reload.Click += async (s, e) => await RefreshAsync();
            details.Click += (s, e) =>
            {
                var claim = Selected();
                if (claim == null) { status.ForeColor = Theme.Danger; status.Text = "Select a claim first."; return; }
                ShowClaimDetails(claim, true);
            };
            grid.CellDoubleClick += (s, e) =>
            {
                if (Selected() is ClaimDto claim) ShowClaimDetails(claim, true);
            };
            approve.Click += async (s, e) => await RunClaimActionAsync(approve, "approve");
            reject.Click += async (s, e) => await RunClaimActionAsync(reject, "reject", true);
            returned.Click += async (s, e) => await RunClaimActionAsync(returned, "record-return");
            _ = RefreshAsync();
        }

        private async void ShowAdminStatistics()
        {
            var page = NewPage();
            var workspace = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Theme.Canvas };
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            workspace.Controls.Add(PageHint("Generate live user, item, claim, and handover counts for an inclusive date range."), 0, 0);
            var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 5, 0, 5) };
            var fromLabel = Theme.FieldLabel("From");
            var from = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(-30), Width = 130 };
            var toLabel = Theme.FieldLabel("To");
            var to = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Today, Width = 130 };
            var generate = new Button { Text = "Generate report" };
            Theme.StyleButton(generate, true);
            controls.Controls.AddRange(new Control[] { fromLabel, from, toLabel, to, generate });
            workspace.Controls.Add(controls, 0, 1);
            var output = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 10f),
                WordWrap = false,
                DetectUrls = false
            };
            workspace.Controls.Add(output, 0, 2);
            var status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Muted, Font = Theme.Body(9f) };
            workspace.Controls.Add(status, 0, 3);
            page.Controls.Add(workspace);
            async Task GenerateAsync()
            {
                if (from.Value.Date > to.Value.Date)
                {
                    status.ForeColor = Theme.Danger;
                    status.Text = "From date must be on or before the to date.";
                    return;
                }
                generate.Enabled = false;
                status.ForeColor = Theme.Muted;
                status.Text = "Querying live data...";
                try
                {
                    var query = "from=" + Q(from.Value.ToString("yyyy-MM-dd")) + "&to=" + Q(to.Value.ToString("yyyy-MM-dd"));
                    var result = await _api.GetAsync<JToken>("v1/admin/reports/summary?" + query);
                    if (result == null) throw new InvalidOperationException("No report data was returned.");
                    output.Text = FormatSummary(result);
                    status.ForeColor = Theme.Success;
                    status.Text = "Date-filtered report generated from the live database.";
                }
                catch (Exception ex) { ShowError(status, ex); }
                finally { generate.Enabled = true; }
            }
            generate.Click += async (s, e) => await GenerateAsync();
            _ = GenerateAsync();
        }

        private (DataGridView Grid, Label Status) AddAdminGridPage(Panel page, Label hint, Control actions)
        {
            var workspace = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Theme.Canvas
            };
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, Math.Max(56, actions.Height)));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            var grid = Theme.Grid();
            var status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Muted, Font = Theme.Body(9f) };
            workspace.Controls.Add(hint, 0, 0);
            workspace.Controls.Add(actions, 0, 1);
            workspace.Controls.Add(grid, 0, 2);
            workspace.Controls.Add(status, 0, 3);
            page.Controls.Add(workspace);
            workspace.BringToFront();
            return (grid, status);
        }

        private static Panel LabeledInput(string caption, Control input, int width)
        {
            var panel = new Panel { Width = width, Height = 46, Margin = new Padding(0, 0, 8, 0) };
            var label = Theme.FieldLabel(caption);
            label.SetBounds(0, 0, width - 4, 16);
            input.SetBounds(0, 18, width - 4, 26);
            panel.Controls.Add(label);
            panel.Controls.Add(input);
            return panel;
        }

        private static string FormatSummary(JToken summary)
        {
            var items = summary["items"];
            var claims = summary["claims"];
            var returns = summary["returns"];
            return string.Join(Environment.NewLine, new[]
            {
                "Reporting period: " + (string)summary["from"] + " to " + (string)summary["to"],
                string.Empty,
                "Users registered: " + Count(summary, "userRegistrations"),
                string.Empty,
                "Items: " + Count(items, "total"),
                "Moderation: " + FormatCountMap(items?["byModerationStatus"], "Pending", "Verified", "Rejected", "Removed"),
                "Item status: " + FormatCountMap(items?["byItemStatus"], "Lost", "Found", "Claimed", "Returned"),
                string.Empty,
                "Claims: " + Count(claims, "total"),
                "Claim status: " + FormatCountMap(claims?["byStatus"], "Pending", "Approved", "Rejected", "Returned"),
                string.Empty,
                "Returns recorded: " + Count(returns, "total")
            });
        }

        private static string FormatCountMap(JToken token, params string[] keys)
        {
            return string.Join("   |   ", keys.Select(key => key + ": " + Count(token, key)));
        }

        private static string Count(JToken token, string key)
        {
            var value = token?[key];
            return value == null ? "0" : value.ToString();
        }
    }

    internal sealed class ItemDetailsForm : Form
    {
        private readonly ItemDto _item;
        private readonly bool _admin;
        private readonly TextBox _evidence = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 90 };
        public string Evidence => _evidence.Text.Trim();

        public ItemDetailsForm(ItemDto item, bool administrator)
        {
            _item = item;
            _admin = administrator;
            Text = "Item details";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(620, item.ItemStatus == "Found" ? 500 : 340);
            BackColor = Theme.Canvas;
            AutoScaleMode = AutoScaleMode.Font;

            var card = new Panel { Bounds = new Rectangle(18, 18, 584, ClientSize.Height - 36), BackColor = Theme.Surface, Padding = new Padding(22), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(card);
            card.Controls.Add(new Label { Text = item.Title ?? "Item", Font = Theme.Body(17f, FontStyle.Bold), ForeColor = Theme.Navy, AutoSize = true, Location = new Point(22, 18) });
            var details = "Category: " + item.Category + Environment.NewLine +
                          "Location: " + item.Location + Environment.NewLine +
                          "Incident date: " + item.IncidentDate + Environment.NewLine +
                          "Report type: " + item.ReportType + Environment.NewLine +
                          "Item status: " + item.ItemStatus + Environment.NewLine +
                          "Moderation: " + item.ModerationStatus + Environment.NewLine +
                          (_admin && !string.IsNullOrWhiteSpace(item.OwnerDisplayName) ? "Submitted by: " + item.OwnerDisplayName + Environment.NewLine : string.Empty) +
                          Environment.NewLine + (item.Description ?? string.Empty);
            var description = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, BackColor = Theme.Surface, ForeColor = Theme.Text, Font = Theme.Body(10f), Text = details, Location = new Point(22, 58), Size = new Size(540, item.ItemStatus == "Found" ? 220 : 150), TabStop = true };
            card.Controls.Add(description);

            if (item.ItemStatus == "Found" && !_admin)
            {
                var label = Theme.FieldLabel("Private proof of ownership");
                label.Location = new Point(22, 286);
                _evidence.SetBounds(22, 308, 540, 90);
                Theme.StyleInput(_evidence);
                card.Controls.Add(label);
                card.Controls.Add(_evidence);
                card.Controls.Add(new Label
                {
                    Text = "Only you and authorized administrators can view this evidence.",
                    AutoSize = true,
                    ForeColor = Theme.Muted,
                    Font = Theme.Body(8.5f),
                    Location = new Point(22, 404)
                });
                var claim = new Button { Text = "Submit claim", DialogResult = DialogResult.OK };
                Theme.StyleButton(claim, true);
                claim.SetBounds(22, 432, 140, 38);
                claim.Click += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(_evidence.Text))
                    {
                        MessageBox.Show(this, "Enter private proof of ownership before submitting.", "Evidence required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        DialogResult = DialogResult.None;
                    }
                };
                card.Controls.Add(claim);
                AcceptButton = claim;
            }

            var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel };
            Theme.StyleButton(close);
            close.SetBounds(472, item.ItemStatus == "Found" ? 432 : 180, 90, 38);
            card.Controls.Add(close);
            CancelButton = close;
        }
    }

    internal sealed class EditItemDialog : Form
    {
        private readonly TextBox _title = new TextBox();
        private readonly TextBox _category = new TextBox();
        private readonly TextBox _description = new TextBox { Multiline = true, Height = 90, ScrollBars = ScrollBars.Vertical };
        private readonly TextBox _location = new TextBox();
        private readonly DateTimePicker _date = new DateTimePicker { Format = DateTimePickerFormat.Short, MaxDate = DateTime.Today };

        public object Changes => new
        {
            title = _title.Text.Trim(), category = _category.Text.Trim(), description = _description.Text.Trim(),
            location = _location.Text.Trim(), incidentDate = _date.Value.ToString("yyyy-MM-dd")
        };

        public EditItemDialog(ItemDto item)
        {
            Text = "Edit report details";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 490);
            BackColor = Theme.Canvas;
            AutoScaleMode = AutoScaleMode.Font;
            var card = new Panel { Bounds = new Rectangle(18, 18, 524, 454), BackColor = Theme.Surface, Padding = new Padding(22) };
            Controls.Add(card);
            card.Controls.Add(new Label { Text = "Edit report", Font = Theme.Body(16f, FontStyle.Bold), ForeColor = Theme.Navy, AutoSize = true, Location = new Point(22, 18) });
            var fields = new (string Label, Control Control, int Top)[]
            {
                ("Title", _title, 68), ("Category", _category, 132), ("Location", _location, 196), ("Incident date", _date, 260), ("Description", _description, 324)
            };
            foreach (var field in fields)
            {
                var label = Theme.FieldLabel(field.Label);
                label.Location = new Point(22, field.Top - 20);
                field.Control.SetBounds(22, field.Top, 460, field.Control == _description ? 78 : 28);
                Theme.StyleInput(field.Control);
                card.Controls.Add(label);
                card.Controls.Add(field.Control);
            }
            _title.Text = item.Title;
            _category.Text = item.Category;
            _location.Text = item.Location;
            _description.Text = item.Description;
            if (DateTime.TryParse(item.IncidentDate, out var date) && date <= DateTime.Today) _date.Value = date;
            var save = new Button { Text = "Save changes", DialogResult = DialogResult.OK };
            Theme.StyleButton(save, true);
            save.SetBounds(22, 414, 140, 36);
            card.Controls.Add(save);
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            Theme.StyleButton(cancel);
            cancel.SetBounds(174, 414, 100, 36);
            card.Controls.Add(cancel);
            AcceptButton = save;
            CancelButton = cancel;
        }
    }

    internal static class TextInputDialog
    {
        public static string Prompt(IWin32Window owner, string title, string prompt, bool required)
        {
            using (var form = new Form
            {
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ClientSize = new Size(460, 230),
                BackColor = Theme.Canvas,
                AutoScaleMode = AutoScaleMode.Font
            })
            {
                var label = new Label { Text = prompt, Location = new Point(22, 20), Size = new Size(416, 42), ForeColor = Theme.Text, Font = Theme.Body(9.5f) };
                var input = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Location = new Point(22, 70), Size = new Size(416, 90) };
                Theme.StyleInput(input);
                var ok = new Button { Text = "Continue", Location = new Point(244, 176), Size = new Size(92, 34) };
                var cancel = new Button { Text = "Cancel", Location = new Point(346, 176), Size = new Size(92, 34), DialogResult = DialogResult.Cancel };
                Theme.StyleButton(ok, true);
                Theme.StyleButton(cancel);
                ok.Click += (s, e) =>
                {
                    if (required && string.IsNullOrWhiteSpace(input.Text))
                    {
                        MessageBox.Show(form, "Enter a reason to continue.", title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        form.DialogResult = DialogResult.None;
                        return;
                    }
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                };
                form.Controls.AddRange(new Control[] { label, input, ok, cancel });
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                if (form.ShowDialog(owner) != DialogResult.OK) return null;
                return input.Text.Trim();
            }
        }
    }
}
