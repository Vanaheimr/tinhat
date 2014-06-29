using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WindowsFormsMouse
{
    public partial class FormMouseInput : Form
    {
        private int oldX = -1;
        private int oldY = -1;
        private byte randByte = 0;
        private byte[] randBytes;
        private int randBytesPos = 0;
        private int bitPos = 0;
        private int gotBytes = 0;
        private int numBytes;
        /// <summary>
        /// numBytes: Reasonable is 16 to 64 or so.
        /// </summary>
        public FormMouseInput(int numBytes)
        {
            if (numBytes < 2)   // Of course, anything less than approx 8 is basically insane
            {
                throw new ArgumentException("numBytes");
            }
            this.numBytes = numBytes;
            this.randBytes = new byte[numBytes];
            InitializeComponent();
        }
        public byte[] GetBytes()
        {
            return randBytes;
        }
        private void UpdateLabelPleaseEnterPrompt()
        {
            string remainingBytesString;
            if (gotBytes >= numBytes)
            {
                buttonOK.Enabled = true;
                remainingBytesString = "(All Done!)";
            }
            else
            {
                buttonOK.Enabled = false;
                remainingBytesString = gotBytes.ToString() + "/" + numBytes.ToString();
            }
            this.labelPleaseEnterPrompt.Text = "Please rapidly and randomly move mouse to satisfy entropy counter: " + remainingBytesString;
        }

        private void FormMouseInput_Load(object sender, EventArgs e)
        {
            UpdateLabelPleaseEnterPrompt();
            this.MouseMove += new MouseEventHandler(MouseMoveGetBit);
        }

        void MouseMoveGetBit(object sender, MouseEventArgs e)
        {
            // It is possible to get repeated events on the same location, if some other
            // application is stealing focus and then returning focus.  I only want to get
            // real events from real movements.
            if (oldX == e.Location.X && oldY == e.Location.Y)
                return;
            double distance = Math.Sqrt(Math.Pow(e.Location.X-oldX,2) + Math.Pow(e.Location.Y-oldY,2));
            oldX = e.Location.X;
            oldY = e.Location.Y;

            // When moving mouse very slowly, there is bias toward changing only the X or only the Y,
            // which introduces bias toward returning 1's more than 0's.  I think.  I don't know.
            if (distance < 6)
                return;

            randByte <<= 1;  // shift left 1 bit
            randByte += (byte)((e.Location.X ^ e.Location.Y)&1); // add in the LSB, thus replacing the LSB
            bitPos++;
            if (bitPos % 8 == 0)
            {
                randBytes[randBytesPos] = (byte)(randBytes[randBytesPos] ^ randByte);
                randByte = 0;
                bitPos = 0;
                if (gotBytes < numBytes)
                {
                    gotBytes++;
                }
                randBytesPos = (randBytesPos + 1) % numBytes;
                UpdateLabelPleaseEnterPrompt();
            }
        }
    }
}
