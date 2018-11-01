using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace GamepadInputGraphOutput
{
    public partial class lockdown : Form
    {

        Form1 form1;
        string id;

        public lockdown()
        {
            InitializeComponent();
        }

        public lockdown(Form1 paramform, string param_id, bool unlocking=false)
        {
            InitializeComponent();
            form1 = paramform;
            form1.set_dialog_result(false);
            id = param_id;

            if(unlocking == false)
            {
                textBox2.Text = "To lock down experiment and prevent unwanted changes to its state, you may want to enter its ID below. Unlocking it also requires you to provide this ID again.";
                this.Text = "Locking Controls";
                button1.Text = "Lock Experiment";
            }
            else
            {
                textBox2.Text = "To change the state of this ongoing experiment to Unlocked, please provide its ID to confirm that you are the owner.";
                this.Text = "Unlocking Controls";
                button1.Text = "Unlock Experiment";
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {

            if (textBox1.Text.Equals(id))
            {
                form1.set_dialog_result(true);
                this.Close();
            }
            else
                MessageBox.Show(this, "Wrong ID!");
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.Close();
        }
    }
}
