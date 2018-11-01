using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace GamepadInputGraphOutput
{
    public partial class admin : Form
    {
        Point mouse_position;

        bool panel1_draw_highlight = false;
        bool panel2_draw_highlight = false;

        int[] actives = new int[360];
        int[] passives = new int[360];

        private void panel1_Paint(object sender, PaintEventArgs e)
        {
            Pen skyblue_pen = new Pen(Color.SkyBlue, 1);
            Pen orange_pen = new Pen(Color.Orange, 1);
            
            Pen highlight_pen = new Pen(Color.Yellow, 1);

            int X1 = 0, Y1 = 0, Y2 = 50;

            for (int i = 0; i < 360; i++)
            {
                if (actives[i] > 0)
                {
                    if (actives[i] == 1)
                        e.Graphics.DrawLine(skyblue_pen, X1 + i, Y1, X1 + i, Y2);
                    else
                        e.Graphics.DrawLine(orange_pen, X1 + i, Y1, X1 + i, Y2);
                }
            }

            if (panel1_draw_highlight == true)
            {
                int i;
                do { i = mouse_position.X; } while (i < 0 || i > 359);
                e.Graphics.DrawLine(highlight_pen, i, Y1, i, Y2);
            }
            else if (panel2_draw_highlight == false)
            {
                label9.Text = "";
                label10.Text = "";
            }

            skyblue_pen.Dispose();
            orange_pen.Dispose();
            highlight_pen.Dispose();
        }

        private void panel2_Paint(object sender, PaintEventArgs e)
        {
            Pen red_pen = new Pen(Color.Red, 1);

            Pen highlight_pen = new Pen(Color.Yellow, 1);

            int X1 = 0, Y1 = 0, Y2 = 50;

            for (int i = 0; i < 360; i++)
                if (passives[i] > 0)
                    e.Graphics.DrawLine(red_pen, X1 + i, Y1, X1 + i, Y2);

            if (panel2_draw_highlight == true)
            {
                int i;
                do { i = mouse_position.X; } while (i < 0 || i > 359);
                e.Graphics.DrawLine(highlight_pen, i, Y1, i, Y2);
            }
            else if (panel1_draw_highlight == false)
            {
                label9.Text = "";
                label10.Text = "";
            }

            red_pen.Dispose();
            highlight_pen.Dispose();
        }


        private void panel1_MouseLeave(object sender, EventArgs e)
        {
            panel1_draw_highlight = false;
            panel1.Invalidate();
        }

        private void panel2_MouseLeave(object sender, EventArgs e)
        {
            panel2_draw_highlight = false;
            panel2.Invalidate();
        }

        private void panel1_MouseMove(object sender, MouseEventArgs e)
        {
            mouse_position = panel1.PointToClient(Control.MousePosition);
            int i;
            do { i = mouse_position.X; } while (i < 0 || i > 359);
            label9.Text = (i * 30).ToString() + "-" + ((i + 1) * 30).ToString() + " Seconds";
            label10.Text = actives[i].ToString() + " Presses";
            panel1.Invalidate();
        }

        private void panel2_MouseMove(object sender, MouseEventArgs e)
        {
            mouse_position = panel2.PointToClient(Control.MousePosition);
            int i;
            do { i = mouse_position.X; } while (i < 0 || i > 359);
            label9.Text = (i * 30).ToString() + "-" + ((i + 1) * 30).ToString() + " Seconds";
            label10.Text = passives[i].ToString() + " Presses";
            panel2.Invalidate();
        }

        Form form1;
        GamepadInputGraphOutput.SqliteHelper sql_helper;
        DataTable dt;

        int elapsed_time;
        int length_multiplier;

        public admin()
        {
            InitializeComponent();
        }

        public admin(Form param_form, GamepadInputGraphOutput.SqliteHelper param_sql_helper)
        {
            InitializeComponent();

            this.Icon = Properties.Resources.maniouse_icon;

            form1 = param_form;
            sql_helper = param_sql_helper;

            string sql = "select * from experiment where state<>'PROGRESSING'";

            dt = sql_helper.GetDataTable(sql);
            dt.Columns[0].ColumnName = "ID";
            dt.Columns[1].ColumnName = "Scientist";
            dt.Columns[2].ColumnName = "Mouse";
            dt.Columns[3].ColumnName = "Actives";
            dt.Columns[4].ColumnName = "Passives";
            dt.Columns[5].ColumnName = "Elapsed Time";
            dt.Columns[6].ColumnName = "Length (Hours)";
            dt.Columns[7].ColumnName = "Total Actives";
            dt.Columns[8].ColumnName = "Total Passives";
            dt.Columns[9].ColumnName = "Total Effective Actives";
            dt.Columns[10].ColumnName = "State";

            dataGridView1.DataSource = dt;

            if (dataGridView1.SelectedRows.Count != 0)
            {
                actives = Array.ConvertAll(((string)dataGridView1.SelectedRows[0].Cells["Actives"].Value).Split(','), int.Parse);
                passives = Array.ConvertAll(((string)dataGridView1.SelectedRows[0].Cells["Passives"].Value).Split(','), int.Parse);
                elapsed_time = int.Parse((string)dataGridView1.SelectedRows[0].Cells["Elapsed Time"].Value);
                length_multiplier = int.Parse((string)dataGridView1.SelectedRows[0].Cells["Length (Hours)"].Value);


                panel1.Invalidate();
                panel2.Invalidate();
                panel7.Invalidate();

            }
        }

        private void admin_FormClosing(object sender, FormClosingEventArgs e)
        {
            //this.Close();
            form1.Show();
        }

        private void dataGridView1_RowEnter(object sender, DataGridViewCellEventArgs e)
        {

            if (dataGridView1.SelectedRows.Count != 0)
            {
                actives = Array.ConvertAll(((string)dataGridView1.SelectedRows[0].Cells["Actives"].Value).Split(','), int.Parse);
                passives = Array.ConvertAll(((string)dataGridView1.SelectedRows[0].Cells["Passives"].Value).Split(','), int.Parse);
                elapsed_time = int.Parse((string)dataGridView1.SelectedRows[0].Cells["Elapsed Time"].Value);
                length_multiplier = int.Parse((string)dataGridView1.SelectedRows[0].Cells["Length (Hours)"].Value);


                panel1.Invalidate();
                panel2.Invalidate();
                panel7.Invalidate();
            }

        }

        private void dataGridView1_RowLeave(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {


            string sql;

            if (dataGridView1.SelectedRows.Count != 0)
            {
                string id = (string)dataGridView1.SelectedRows[0].Cells["id"].Value;

                if (MessageBox.Show(this, "Are you sure you want to permanently delete the experiment \"" + id + "\"?", "Closing", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    sql = "select count(*) from experiment where id = '" + id + "'";
                    if (!sql_helper.ExecuteScalar(sql).Equals("0"))
                    {

                        sql = "delete from experiment where id = '" + id + "'";
                        sql_helper.ExecuteNonQuery(sql);

                        sql = "select * from experiment where state<>'PROGRESSING'";
                        dt = sql_helper.GetDataTable(sql);
                        dataGridView1.DataSource = dt;

                        if (dataGridView1.SelectedRows.Count != 0)
                        {
                            actives = Array.ConvertAll(((string)dataGridView1.SelectedRows[0].Cells["Actives"].Value).Split(','), int.Parse);
                            passives = Array.ConvertAll(((string)dataGridView1.SelectedRows[0].Cells["Passives"].Value).Split(','), int.Parse);
                            elapsed_time = int.Parse((string)dataGridView1.SelectedRows[0].Cells["Elapsed Time"].Value);
                            length_multiplier = int.Parse((string)dataGridView1.SelectedRows[0].Cells["Length (Hours)"].Value);

                            panel1.Invalidate();
                            panel2.Invalidate();
                            panel7.Invalidate();
                        }
                    }
                }
            }


        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count == 0)
                return;

            string id = (string)dataGridView1.SelectedRows[0].Cells["id"].Value;

            new exportprefs(sql_helper, id).ShowDialog();
        }

        private void button2_Click(object sender, EventArgs e)
        {


            if (dataGridView1.SelectedRows.Count == 0)
                return;

            string id = (string)dataGridView1.SelectedRows[0].Cells["id"].Value;
            string sql = "select count(*) from experiment where id = '" + id + "'";

            if (sql_helper.ExecuteScalar(sql).Equals("0"))
                return;

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

            sb.Append("id,scientist,mouse,");

            for (int i = 0; i < 360; i++)
                sb.Append((i * 30).ToString() + "-" + ((i + 1) * 30).ToString() + "-Actives,");
            for (int i = 0; i < 360; i++)
                sb.Append((i * 30).ToString() + "-" + ((i + 1) * 30).ToString() + "-Passives,");

            sb.AppendLine("elapsedtime,lengthmultiplier,totalactives,totalpassives,totaleffectiveactives,state");

            foreach (DataRow row in dt.Rows)
            {
                string[] fields = row.ItemArray.Select(field => field.ToString()).ToArray();
                sb.AppendLine(string.Join(",", fields));
            }

            File.WriteAllText(sourceFile, sb.ToString());

            MessageBox.Show(this, "File " + inputFile + " was successfully written to \"" + sourcedir + "\"", "Success");
        }

        private void panel1_MouseEnter(object sender, EventArgs e)
        {
            mouse_position = panel1.PointToClient(Control.MousePosition);
            int i;
            do { i = mouse_position.X; } while (i < 0 || i > 359);
            panel1_draw_highlight = true;
            label9.Text = (i * 30).ToString() + "-" + ((i + 1) * 30).ToString() + " Seconds";
            label10.Text = actives[i].ToString() + " Presses";
            panel1.Invalidate();
        }

        private void panel2_MouseEnter(object sender, EventArgs e)
        {
            mouse_position = panel2.PointToClient(Control.MousePosition);
            int i;
            do { i = mouse_position.X; } while (i < 0 || i > 359);
            panel2_draw_highlight = true;
            label9.Text = (i * 30).ToString() + "-" + ((i + 1) * 30).ToString() + " Seconds";
            label10.Text = passives[i].ToString() + " Presses";
            panel2.Invalidate();
        }

        private void panel7_Paint(object sender, PaintEventArgs e)
        {
            Pen black_pen = new Pen(Color.Black, 1);

            int tp = (120 * length_multiplier)-1;   //termination point
            int etid = elapsed_time / 30;    //elapsed_time_indicator_diversion

            int X1 = etid, Y1 = 0, Y2 = 200;

            Point[] DOWN = new Point[] { new Point(X1 - 7, 0), new Point(X1 + 7 + 1, 0), new Point(X1, 7) };
            Point[] UP = new Point[] { new Point(X1 - 7, 121), new Point(X1 + 7, 121), new Point(X1, 113) };

            e.Graphics.DrawLine(black_pen, X1, Y1, X1, Y2);

            e.Graphics.DrawLine(black_pen, tp, Y1, tp, Y2);

            using (SolidBrush brush = new SolidBrush(Color.Black))
            {
                e.Graphics.FillPolygon(brush, DOWN);
                e.Graphics.FillPolygon(brush, UP);
            }

            black_pen.Dispose();
        }
    }
}
