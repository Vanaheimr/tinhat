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
            var myForm = new FormKeyboardInputPrompt(128);
            DialogResult result = myForm.ShowDialog();
            if (result == DialogResult.OK)
            {
                MessageBox.Show("Got string: '" + myForm.GetUserString() + "'");
            }
            else
            {
                MessageBox.Show("No result received");
            }
        }
    }
}
