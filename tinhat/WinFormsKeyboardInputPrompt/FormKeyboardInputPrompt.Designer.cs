namespace WinFormsKeyboardInputPrompt
{
    partial class FormKeyboardInputPrompt
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.labelPleaseEnterPrompt = new System.Windows.Forms.Label();
            this.textBoxUserChars = new System.Windows.Forms.TextBox();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonOK = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // labelPleaseEnterPrompt
            // 
            this.labelPleaseEnterPrompt.AutoSize = true;
            this.labelPleaseEnterPrompt.Location = new System.Drawing.Point(13, 13);
            this.labelPleaseEnterPrompt.Name = "labelPleaseEnterPrompt";
            this.labelPleaseEnterPrompt.Size = new System.Drawing.Size(35, 13);
            this.labelPleaseEnterPrompt.TabIndex = 0;
            this.labelPleaseEnterPrompt.Text = "label1";
            // 
            // textBoxUserChars
            // 
            this.textBoxUserChars.AcceptsReturn = true;
            this.textBoxUserChars.AcceptsTab = true;
            this.textBoxUserChars.Location = new System.Drawing.Point(13, 30);
            this.textBoxUserChars.Multiline = true;
            this.textBoxUserChars.Name = "textBoxUserChars";
            this.textBoxUserChars.Size = new System.Drawing.Size(564, 222);
            this.textBoxUserChars.TabIndex = 1;
            // 
            // buttonCancel
            // 
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(501, 279);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 2;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            // 
            // buttonOK
            // 
            this.buttonOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.buttonOK.Enabled = false;
            this.buttonOK.Location = new System.Drawing.Point(420, 279);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.TabIndex = 3;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            // 
            // FormKeyboardInputPrompt
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(589, 314);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.textBoxUserChars);
            this.Controls.Add(this.labelPleaseEnterPrompt);
            this.Name = "FormKeyboardInputPrompt";
            this.Text = "Random Keyboard Input";
            this.Load += new System.EventHandler(this.FormKeyboardInputPrompt_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelPleaseEnterPrompt;
        private System.Windows.Forms.TextBox textBoxUserChars;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Button buttonOK;
    }
}

