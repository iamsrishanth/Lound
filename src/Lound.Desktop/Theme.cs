using System.Drawing;
using System.Windows.Forms;

namespace Lound.Desktop
{
    internal static class Theme
    {
        public static readonly Color Navy = Color.FromArgb(22, 39, 61);
        public static readonly Color NavyLight = Color.FromArgb(39, 61, 86);
        public static readonly Color Gold = Color.FromArgb(235, 193, 54);
        public static readonly Color Green = Color.FromArgb(31, 103, 84);
        public static readonly Color Canvas = Color.FromArgb(243, 246, 249);
        public static readonly Color Surface = Color.FromArgb(252, 253, 254);
        public static readonly Color Text = Color.FromArgb(29, 42, 57);
        public static readonly Color Muted = Color.FromArgb(87, 103, 121);
        public static readonly Color Border = Color.FromArgb(216, 224, 232);
        public static readonly Color Danger = Color.FromArgb(157, 47, 53);
        public static readonly Color Success = Color.FromArgb(35, 112, 78);

        public static Font Body(float size = 9.5f, FontStyle style = FontStyle.Regular) => new Font("Segoe UI", size, style);

        public static void StyleButton(Button button, bool primary = false, bool danger = false)
        {
            var baseColor = danger ? Danger : primary ? Green : Surface;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = primary || danger ? 0 : 1;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.MouseOverBackColor = danger
                ? Color.FromArgb(139, 38, 44)
                : primary ? Color.FromArgb(25, 88, 71) : Color.FromArgb(237, 242, 247);
            button.FlatAppearance.MouseDownBackColor = danger
                ? Color.FromArgb(124, 33, 39)
                : primary ? Color.FromArgb(21, 75, 62) : Color.FromArgb(226, 234, 241);
            button.BackColor = baseColor;
            button.ForeColor = primary || danger ? Surface : Navy;
            button.Font = Body(9.5f, FontStyle.Bold);
            button.Height = 38;
            button.MinimumSize = new Size(100, 38);
            button.Padding = new Padding(14, 0, 14, 0);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
        }

        public static void StyleInput(Control control)
        {
            control.Font = Body(10f);
            control.ForeColor = Text;
            control.BackColor = Surface;
            if (control is TextBox textBox)
            {
                textBox.BorderStyle = BorderStyle.FixedSingle;
                textBox.Margin = new Padding(0, 3, 0, 8);
            }
            if (control is ComboBox comboBox)
            {
                comboBox.FlatStyle = FlatStyle.Flat;
                comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
                comboBox.Margin = new Padding(0, 3, 0, 8);
            }
        }

        public static Label FieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = Body(9f, FontStyle.Bold),
                ForeColor = Muted,
                Margin = new Padding(0, 8, 0, 2)
            };
        }

        public static DataGridView Grid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Surface,
                BorderStyle = BorderStyle.None,
                GridColor = Border,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Navy,
                    ForeColor = Surface,
                    Font = Body(9f, FontStyle.Bold),
                    Padding = new Padding(10, 6, 8, 6),
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    SelectionBackColor = Navy,
                    SelectionForeColor = Surface
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Surface,
                    ForeColor = Text,
                    SelectionBackColor = Color.FromArgb(221, 235, 239),
                    SelectionForeColor = Navy,
                    Padding = new Padding(10, 5, 8, 5),
                    Font = Body(9.3f)
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(247, 249, 251) },
                RowHeadersVisible = false,
                ReadOnly = true,
                AutoGenerateColumns = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ColumnHeadersHeight = 42,
                RowTemplate = { Height = 40 }
            };
            grid.CellFormatting += (sender, e) =>
            {
                if (e.Value == null) return;
                var value = e.Value.ToString();
                if (value == "Verified" || value == "Returned" || value == "Approved" || value == "Found")
                    e.CellStyle.ForeColor = Success;
                else if (value == "Pending" || value == "Lost")
                    e.CellStyle.ForeColor = Color.FromArgb(153, 104, 13);
                else if (value == "Rejected" || value == "Removed")
                    e.CellStyle.ForeColor = Danger;
                else if (value == "Claimed")
                    e.CellStyle.ForeColor = NavyLight;
            };
            return grid;
        }
    }
}
