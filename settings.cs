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
    public partial class settings : Form
    {

        GamepadInputGraphOutput.SqliteHelper sql_helper;

        public settings()
        {
            InitializeComponent();
        }

        public settings(GamepadInputGraphOutput.SqliteHelper param_sql_helper)
        {
            InitializeComponent();
            sql_helper = param_sql_helper;

            string sql;

            sql = "select defaultxplengthmultiplier from application";
            int current_length_multiplier = int.Parse(sql_helper.ExecuteScalar(sql));
            comboBox1.SelectedIndex = current_length_multiplier - 1;

            sql = "select defaultmultiplierforexportintervals from application";
            int current_length_of_export_multiplier = int.Parse(sql_helper.ExecuteScalar(sql));
            comboBox2.SelectedIndex = current_length_of_export_multiplier - 1;

        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string sql = "UPDATE application SET defaultxplengthmultiplier='" + (comboBox1.SelectedIndex + 1).ToString() + "', defaultmultiplierforexportintervals='" + (comboBox2.SelectedIndex + 1).ToString() + "';";
            sql_helper.ExecuteNonQuery(sql);
            this.Close();
        }
    }
}