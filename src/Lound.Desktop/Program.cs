using System;
using System.Windows.Forms;

namespace Lound.Desktop
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            while (true)
            {
                using (var login = new LoginForm())
                {
                    if (login.ShowDialog() != DialogResult.OK)
                        return;

                    using (var main = new MainForm(login.Session))
                    {
                        Application.Run(main);
                        if (!main.SignedOut)
                            return;
                    }
                }
            }
        }
    }
}
