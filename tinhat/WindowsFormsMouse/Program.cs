using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace WindowsFormsMouse
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
            var myForm = new FormMouseInput(32);   // Request 32 bytes, ~256 bits of entropy
            DialogResult result = myForm.ShowDialog();
            if (result == DialogResult.OK)
            {
                byte[] resultBytes = myForm.GetBytes();
                MessageBox.Show("Got bytes: '" + BitConverter.ToString(resultBytes) + "'");
            }
            else
            {
                MessageBox.Show("No result received");
            }
        }
    }
}
