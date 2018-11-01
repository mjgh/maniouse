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
    public partial class createpopup : Form
    {
        string id;

        public createpopup()
        {
            InitializeComponent();
        }

        public createpopup(string param_id)
        {
            InitializeComponent();

            id = param_id;

            textBox1.Text = "To access the results of your experiment in the future you have to write down and keep the following code:";
            textBox2.Text = "In case you happen to lose this code, you may contact the person with admin rights.";
            textBox3.Text = id;

        }
        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
