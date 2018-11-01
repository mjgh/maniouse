using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GamepadInputGraphOutput
{
    public partial class load : Form
    {
        Form1 form1;

        public load()
        {
            InitializeComponent();
        }

        public load(Form1 paramform)
        {
            InitializeComponent();
            form1 = paramform;
            form1.set_dialog_result(false);

        }

        private void button1_Click(object sender, EventArgs e)
        {
            form1.set_load_id(textBox1.Text);
            form1.set_dialog_result(true);
            this.Close(); 
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.Close();
        }
    }
}
