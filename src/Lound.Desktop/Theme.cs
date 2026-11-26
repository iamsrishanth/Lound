using System.Drawing;
using System.Windows.Forms;

namespace Lound.Desktop
{
    internal static class Theme
    {
        public static readonly Color Navy = Color.FromArgb(18, 43, 78);
        public static readonly Color NavyLight = Color.FromArgb(36, 65, 103);
        public static readonly Color Gold = Color.FromArgb(232, 190, 35);
        public static readonly Color Green = Color.FromArgb(39, 119, 86);
        public static readonly Color Canvas = Color.FromArgb(244, 247, 249);
        public static readonly Color Surface = Color.FromArgb(250, 251, 252);
        public static readonly Color Text = Color.FromArgb(28, 41, 57);
        public static readonly Color Muted = Color.FromArgb(92, 107, 124);
        public static readonly Color Border = Color.FromArgb(211, 220, 229);
        public static readonly Color Danger = Color.FromArgb(172, 48, 54);
        public static readonly Color Success = Color.FromArgb(35, 112, 78);

        public static Font Body(float size = 9.5f, FontStyle style = FontStyle.Regular) => new Font("Segoe UI", size, style);

        public static void StyleButton(Button button, bool primary = false, bool danger = false)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = primary || danger ? 0 : 1;
            button.FlatAppearance.BorderColor = Border;
            button.BackColor = danger ? Danger : primary ? Green : Surface;
            button.ForeColor = primary || danger ? Surface : Navy;
            button.Font = Body(9.5f, FontStyle.Bold);
            button.Height = 36;
            button.MinimumSize = new Size(96, 36);
            button.Padding = new Padding(12, 0, 12, 0);
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
                textBox.Margin = new Padding(0, 2, 0, 10);
            }
            if (control is ComboBox comboBox)
            {
                comboBox.FlatStyle = FlatStyle.Flat;
                comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
                comboBox.Margin = new Padding(0, 2, 0, 10);
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
                Margin = new Padding(0, 8, 0, 1)
            };
        }

        public static DataGridView Grid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Surface,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Border,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Navy,
                    ForeColor = Surface,
                    Font = Body(9f, FontStyle.Bold),
                    Padding = new Padding(6, 4, 6, 4)
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Surface,
                    ForeColor = Text,
                    SelectionBackColor = Color.FromArgb(221, 232, 243),
                    SelectionForeColor = Navy,
                    Padding = new Padding(5, 4, 5, 4),
                    Font = Body(9.3f)
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 250, 252) },
                RowHeadersVisible = false,
                ReadOnly = true,
                AutoGenerateColumns = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ColumnHeadersHeight = 38,
                RowTemplate = { Height = 34 }
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
