using System;
using System.Windows.Forms;

namespace MaxLifx
{
    internal static class Program
    {
        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            string loadProfileName = null;
            if (args.Length > 1 && args[0].Contains("--load"))
            {
                loadProfileName = args[1];
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(loadProfileName));
        }
    }
}