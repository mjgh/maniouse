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
    public partial class adminpass : Form
    {
        Form form1;
        GamepadInputGraphOutput.SqliteHelper sql_helper;
        bool logged_in = false;

        public adminpass()
        {
            InitializeComponent();
        }

        public adminpass(Form param_form, GamepadInputGraphOutput.SqliteHelper param_sql_helper)
        {
            InitializeComponent();
            form1 = param_form;
            sql_helper = param_sql_helper;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string sql = "select count(*) from application where adminpass = '" + textBox1.Text + "'";

            if (!sql_helper.ExecuteScalar(sql).Equals("0"))
            {
                new admin(form1, sql_helper).Show();
                logged_in = true;
                this.Close();
                
            }

            else
            {
                MessageBox.Show(this, "Wrong Password!", "Access Error", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                form1.Show();
                this.Close();

            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            //this.Close();
            new changepassword(form1, sql_helper).ShowDialog();

        }

        private void adminpass_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (logged_in == false)
                form1.Show();

        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.Close();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}
