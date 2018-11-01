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
    public partial class changepassword : Form
    {

        Form form1;
        GamepadInputGraphOutput.SqliteHelper sql_helper;

        public changepassword()
        {
            InitializeComponent();
        }

        public changepassword(Form param_form, GamepadInputGraphOutput.SqliteHelper param_sql_helper)
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
                if (textBox2.Text.Equals(textBox3.Text))
                {

                    sql = "UPDATE application SET adminpass= '" + textBox2.Text + "'";
                    sql_helper.ExecuteNonQuery(sql);

                    this.Close();
                }
                else
                    MessageBox.Show(this, "Repeated Password does not match the New Password!", "Password Modification Error", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
            }
            else
                MessageBox.Show(this, "Current Password Wrong!", "Password Modification Error", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.Close();
        }
    }
}
