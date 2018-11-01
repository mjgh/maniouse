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
    public partial class create : Form
    {
        Form1 form1;
        GamepadInputGraphOutput.SqliteHelper sql_helper;

        public create()
        {
            InitializeComponent();
        }

        public create(Form1 paramform, GamepadInputGraphOutput.SqliteHelper param_sql_helper)
        {
            InitializeComponent();
            form1 = paramform;
            sql_helper = param_sql_helper;

            string sql;

            sql = "select defaultxplengthmultiplier from application";
            int current_length_multiplier = int.Parse(sql_helper.ExecuteScalar(sql));
            comboBox1.SelectedIndex = current_length_multiplier - 1;


            form1.set_dialog_result(false);

        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (textBox1.Text.Equals("") || textBox2.Text.Equals(""))
            {
                MessageBox.Show(this, "None of the fields can be empty. Please provide both names.", "Create Error");
                return;
            }

            form1.set_new_scientist_mouse_length(textBox1.Text, textBox2.Text, comboBox1.SelectedIndex);
            form1.set_dialog_result(true);
            this.Close();
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.Close();
        }
    }
}
