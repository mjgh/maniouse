using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace GamepadInputGraphOutput
{
    public partial class exportprefs : Form
    {

        GamepadInputGraphOutput.SqliteHelper sql_helper;
        string id;
        int interval_length_multiplier;

        public exportprefs()
        {
            InitializeComponent();
        }

        public exportprefs(GamepadInputGraphOutput.SqliteHelper param_sql_helper, string param_id)
        {
            InitializeComponent();
            sql_helper = param_sql_helper;
            id = param_id;
            string sql = "select defaultmultiplierforexportintervals from application";
            comboBox2.SelectedIndex = int.Parse(sql_helper.ExecuteScalar(sql)) - 1;

        }

        private void button1_Click(object sender, EventArgs e)
        {
            DataTable xp_meta;
            string sql;
            int i;
            int cumulative_actives;
            int cumulative_effective_actives;
            int cumulative_passives;

            int interval_length, length_multiplier, total_intervals;




            sql = "select count(*) from experiment where id = '" + id + "'";

            if (sql_helper.ExecuteScalar(sql).Equals("0"))
                return;

            sql = "select scientist, mouse, elapsedtime, lengthmultiplier, totalactives, totalpassives, totaleffectiveactives, state from experiment where id='" + id + "'";
            xp_meta = sql_helper.GetDataTable(sql);

            sql = "select actives from experiment where id = '" + id + "'";
            int[] actives = Array.ConvertAll(sql_helper.ExecuteScalar(sql).Split(','), int.Parse);

            sql = "select passives from experiment where id = '" + id + "'";
            int[] passives = Array.ConvertAll(sql_helper.ExecuteScalar(sql).Split(','), int.Parse);


            string sourcedir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Maniouse");
            string inputFile = "Export-" + DateTime.Now.Ticks + ".csv";

            string sourceFile = Path.Combine(sourcedir, inputFile);

            if (!File.Exists(sourceFile))
            {
                if (!Directory.Exists(sourcedir))
                    Directory.CreateDirectory(sourcedir);

            }
            else
                return;

            StringBuilder sb = new StringBuilder();



            string[] fields = xp_meta.Rows[0].ItemArray.Select(field => field.ToString()).ToArray();

            interval_length_multiplier = comboBox2.SelectedIndex + 1;
            interval_length = interval_length_multiplier * 10;
            length_multiplier = int.Parse(fields[3]);
            total_intervals = (length_multiplier * 120) / interval_length;

            string scientist = fields[0];
            string mouse = fields[1];
            string elapsed_time = fields[2];
            string length = length_multiplier.ToString() + " Hours";
            string total_actives = fields[4];
            string total_passives = fields[5];
            string total_effective_actives = fields[6];
            string state = fields[7];

            sb.AppendLine("Scientist," + scientist + ",,,,,,,,,,,");
            sb.AppendLine("Mouse," + mouse + ",,,,,,,,,,,");
            sb.AppendLine("Elapsed Time," + elapsed_time + ",,,,,,,,,,,");
            sb.AppendLine("Length," + length + ",,,,,,,,,,,");
            sb.AppendLine("Actives (Total)," + total_actives + ",,,,,,,,,,,");
            sb.AppendLine("Passives (Total)," + total_passives + ",,,,,,,,,,,");
            sb.AppendLine("E. Actives (Total)," + total_effective_actives + ",,,,,,,,,,,");
            sb.AppendLine("State," + state + ",,,,,,,,,,,");
            sb.AppendLine(",,,,,,,,,,,,");

            sb.Append(",");

            i = 0;
            while (true)
            {

                sb.Append((i * (interval_length_multiplier * 5)).ToString() + "-" + ((i + 1) * (interval_length_multiplier * 5)).ToString() + "Mins");

                i++;

                if (i == total_intervals)
                    break;
                else
                    sb.Append(",");
            }

            sb.AppendLine();

            sb.Append("Actives,");

            i = 0;
            while (true)
            {
                cumulative_actives = 0;

                for (int j = i * interval_length; j < (i + 1) * interval_length; j++)
                {
                    cumulative_actives += actives[j];
                }

                sb.Append(cumulative_actives.ToString());

                i++;

                if (i == total_intervals)
                    break;
                else
                    sb.Append(",");
            }

            sb.AppendLine();

            sb.Append("E. Actives,");

            i = 0;
            while (true)
            {
                cumulative_effective_actives = 0;

                for (int j = i * interval_length; j < (i + 1) * interval_length; j++)
                {
                    if (actives[j] > 0)
                        cumulative_effective_actives++;
                }

                sb.Append(cumulative_effective_actives.ToString());

                i++;

                if (i == total_intervals)
                    break;
                else
                    sb.Append(",");
            }

            sb.AppendLine();

            sb.Append("Passives,");

            i = 0;
            while (true)
            {
                cumulative_passives = 0;

                for (int j = i * interval_length; j < (i + 1) * interval_length; j++)
                {
                    cumulative_passives += passives[j];

                }

                sb.Append(cumulative_passives.ToString());

                i++;

                if (i == total_intervals)
                    break;
                else
                    sb.Append(",");
            }

            sb.AppendLine();

            File.WriteAllText(sourceFile, sb.ToString());

            MessageBox.Show(this, "File " + inputFile + " was successfully written to \"" + sourcedir + "\"", "Success");

            this.Close();
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.Close();

        }
    }
}
