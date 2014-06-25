using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace WinFormsKeyboardInputPrompt
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // We never expect to run this Program directly, except maybe during debugging.
            // Normally, some other program references us, and instantiates new FormKeyboardInputPrompt
            // within their own context.  So I just hard-code some argument in the following
            // line, for the sake of averting compile error.
            Application.Run(new FormKeyboardInputPrompt(128));
        }
    }
}
