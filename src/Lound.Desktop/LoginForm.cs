using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Lound.Desktop
{
    internal sealed class LoginForm : Form
    {
        private const string DefaultApiUrl = "http://localhost:5080";
        private readonly TextBox _apiUrl = new TextBox();
        private readonly TextBox _email = new TextBox();
        private readonly TextBox _password = new TextBox { UseSystemPasswordChar = true };
        private readonly Label _message = new Label();
        private readonly Button _signIn = new Button { Text = "Sign in" };
        private readonly Panel _card;

        public ClientSession Session { get; private set; }

        public LoginForm()
        {
            Text = "Lound | Sign in";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(500, 660);
            MinimumSize = new Size(500, 660);
            BackColor = Theme.Canvas;
            Font = Theme.Body();
            AutoScaleMode = AutoScaleMode.Font;

            _card = new Panel
            {
                BackColor = Theme.Surface,
                Size = new Size(400, 540),
                Anchor = AnchorStyles.None,
                Padding = new Padding(32)
            };
            Controls.Add(_card);

            var title = new Label
            {
                Text = "LOUND",
                Font = Theme.Body(24f, FontStyle.Bold),
                ForeColor = Theme.Navy,
                AutoSize = true,
                Location = new Point(32, 25)
            };
            _card.Controls.Add(title);
            _card.Controls.Add(new Label
            {
                Text = "Lost and Found Management",
                Font = Theme.Body(11f),
                ForeColor = Theme.Muted,
                AutoSize = true,
                Location = new Point(34, 78)
            });
            var accent = new Panel { BackColor = Theme.Gold, Location = new Point(34, 112), Size = new Size(54, 4) };
            _card.Controls.Add(accent);

            _apiUrl.SetBounds(34, 158, 330, 30);
            _email.SetBounds(34, 231, 330, 30);
            _password.SetBounds(34, 304, 330, 30);
            Theme.StyleInput(_apiUrl);
            Theme.StyleInput(_email);
            Theme.StyleInput(_password);
            var apiLabel = Theme.FieldLabel("API address");
            var emailLabel = Theme.FieldLabel("Email");
            var passwordLabel = Theme.FieldLabel("Password");
            apiLabel.Location = new Point(34, 135);
            _apiUrl.Location = new Point(34, 158);
            emailLabel.Location = new Point(34, 208);
            _email.Location = new Point(34, 231);
            passwordLabel.Location = new Point(34, 281);
            _password.Location = new Point(34, 304);
            _card.Controls.AddRange(new Control[] { apiLabel, _apiUrl, emailLabel, _email, passwordLabel, _password });

            _message.SetBounds(34, 346, 330, 44);
            _message.ForeColor = Theme.Danger;
            _message.Font = Theme.Body(9f);
            _card.Controls.Add(_message);

            Theme.StyleButton(_signIn, true);
            _signIn.SetBounds(34, 402, 330, 42);
            _signIn.Click += async (s, e) => await SignInAsync();
            _card.Controls.Add(_signIn);
            AcceptButton = _signIn;

            var register = new LinkLabel
            {
                Text = "Create a standard user account",
                LinkColor = Theme.Navy,
                ActiveLinkColor = Theme.Green,
                Font = Theme.Body(9.5f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(34, 462)
            };
            register.LinkClicked += (s, e) => OpenRegistration();
            _card.Controls.Add(register);

            ClientSizeChanged += (s, e) => CenterCard();
            CenterCard();

            _apiUrl.Text = LoadApiUrl();
            Shown += (s, e) => _email.Focus();
        }

        private async Task SignInAsync()
        {
            _message.Text = string.Empty;
            if (string.IsNullOrWhiteSpace(_email.Text) || string.IsNullOrWhiteSpace(_password.Text))
            {
                _message.Text = "Enter your email and password.";
                return;
            }

            _signIn.Enabled = false;
            _signIn.Text = "Signing in...";
            try
            {
                var api = new ApiClient(_apiUrl.Text);
                try
                {
                    var login = await api.LoginAsync(_email.Text.Trim(), _password.Text);
                    SaveApiUrl(_apiUrl.Text);
                    Session = new ClientSession { Api = api, User = login.User };
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch
                {
                    api.Dispose();
                    throw;
                }
            }
            catch (ApiException ex)
            {
                _message.Text = ex.Message;
                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    _password.Clear();
            }
            catch (Exception ex)
            {
                _message.Text = ex is ArgumentException ? ex.Message : "Could not reach the API. Check that it is running and the address is correct.";
            }
            finally
            {
                _signIn.Enabled = true;
                _signIn.Text = "Sign in";
            }
        }

        private void CenterCard()
        {
            if (_card == null) return;
            _card.Location = new Point(Math.Max(12, (ClientSize.Width - _card.Width) / 2), Math.Max(12, (ClientSize.Height - _card.Height) / 2));
        }

        private void OpenRegistration()
        {
            _message.Text = string.Empty;
            try
            {
                using (var registration = new RegisterForm(_apiUrl.Text))
                    registration.ShowDialog(this);
            }
            catch (Exception ex)
            {
                _message.Text = ex.Message;
            }
        }

        private static string SettingsPath
        {
            get
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lound");
                Directory.CreateDirectory(folder);
                return Path.Combine(folder, "client-settings.txt");
            }
        }

        private static string LoadApiUrl()
        {
            try
            {
                var path = SettingsPath;
                if (File.Exists(path))
                {
                    var value = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
            }
            catch { }
            return DefaultApiUrl;
        }

        private static void SaveApiUrl(string value)
        {
            try { File.WriteAllText(SettingsPath, (value ?? DefaultApiUrl).Trim()); }
            catch { }
        }
    }

    internal sealed class RegisterForm : Form
    {
        private readonly ApiClient _api;
        private readonly TextBox _name = new TextBox();
        private readonly TextBox _email = new TextBox();
        private readonly TextBox _password = new TextBox { UseSystemPasswordChar = true };
        private readonly TextBox _confirm = new TextBox { UseSystemPasswordChar = true };
        private readonly Label _message = new Label();
        private readonly Button _create = new Button { Text = "Create account" };

        public RegisterForm(string apiUrl)
        {
            _api = new ApiClient(apiUrl);
            Text = "Lound | Create account";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(450, 500);
            BackColor = Theme.Canvas;
            AutoScaleMode = AutoScaleMode.Font;

            var card = new Panel { BackColor = Theme.Surface, Bounds = new Rectangle(25, 22, 400, 450), Padding = new Padding(28) };
            Controls.Add(card);
            card.Controls.Add(new Label { Text = "Create your account", Font = Theme.Body(17f, FontStyle.Bold), ForeColor = Theme.Navy, AutoSize = true, Location = new Point(28, 24) });

            var fields = new[]
            {
                ("Display name", _name, 78), ("Email", _email, 148), ("Password", _password, 218), ("Confirm password", _confirm, 288)
            };
            foreach (var field in fields)
            {
                var label = Theme.FieldLabel(field.Item1);
                label.Location = new Point(28, field.Item3 - 20);
                field.Item2.SetBounds(28, field.Item3, 340, 28);
                Theme.StyleInput(field.Item2);
                card.Controls.Add(label);
                card.Controls.Add(field.Item2);
            }

            _message.SetBounds(28, 330, 340, 42);
            _message.ForeColor = Theme.Danger;
            card.Controls.Add(_message);
            Theme.StyleButton(_create, true);
            _create.SetBounds(28, 378, 340, 40);
            _create.Click += async (s, e) => await CreateAsync();
            card.Controls.Add(_create);
            AcceptButton = _create;
            CancelButton = new Button { DialogResult = DialogResult.Cancel };
            FormClosed += (s, e) => _api.Dispose();
        }

        private async Task CreateAsync()
        {
            _message.ForeColor = Theme.Danger;
            _message.Text = string.Empty;
            if (string.IsNullOrWhiteSpace(_name.Text) || string.IsNullOrWhiteSpace(_email.Text) || string.IsNullOrWhiteSpace(_password.Text))
            {
                _message.Text = "Complete every field.";
                return;
            }
            if (_password.Text != _confirm.Text)
            {
                _message.Text = "Passwords do not match.";
                return;
            }

            _create.Enabled = false;
            _create.Text = "Creating...";
            try
            {
                await _api.RegisterAsync(_email.Text.Trim(), _password.Text, _name.Text.Trim());
                _message.ForeColor = Theme.Success;
                _message.Text = "Account created. Return to sign in.";
                _password.Clear();
                _confirm.Clear();
            }
            catch (ApiException ex) { _message.Text = ex.Message; }
            catch (Exception) { _message.Text = "Could not reach the API. Check that it is running."; }
            finally
            {
                _create.Enabled = true;
                _create.Text = "Create account";
            }
        }
    }
}
