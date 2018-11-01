using SharpDX.DirectInput;
using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Security.Cryptography;
using System.Drawing.Drawing2D;

namespace GamepadInputGraphOutput
{

    public partial class Form1 : Form
    {
        static bool last_dialog_success;
        string load_id;
        string new_scientist;
        string new_mouse;
        int new_xp_length_multiplier;

        private enum xp_states { NOTINITIALIZED, PAUSED, PROGRESSING, DONE };
        static private xp_states xp1_state = xp_states.NOTINITIALIZED;
        static private xp_states xp2_state = xp_states.NOTINITIALIZED;

        struct xp_data
        {
            public int[] actives;
            public int[] passives;
        }
        private xp_data xp1_data = new xp_data();
        private xp_data xp2_data = new xp_data();
        private int xp1_actives_total;
        private int xp1_passives_total;
        private int xp1_effective_actives_total;
        private int xp2_actives_total;
        private int xp2_passives_total;
        private int xp2_effective_actives_total;
        private int xp1_length_multiplier;
        private int xp2_length_multiplier;
        private double xp1_offset_time;
        private double xp2_offset_time;
        private int xp1_to_active;
        private int xp2_to_active;
        private DateTime xp1_start_time;
        private DateTime xp2_start_time;
        static private string xp1_id;
        static private string xp2_id;
        static Thread xp1_thread;
        static Thread xp2_thread;
        static SqliteHelper sql_helper;
        string xp1_scientist;
        string xp2_scientist;
        string xp1_mouse;
        string xp2_mouse;
        bool checkBox3_programmatically_checked_change;
        bool checkBox4_programmatically_checked_change;

        static private int const_3600 = 3600;
        //static private int const_360 = 360;

        private Object[] xp_data_update_lock = new Object[4];
        static string[] xp1_data_lock = new string[2];
        static string[] xp2_data_lock = new string[2];
        static string xp1_state_lock;
        static string xp2_state_lock;
        static string thread1_lock;
        static string thread2_lock;
        static string main_thread_lock;
        static string xp1_to_active_lock;
        static string xp2_to_active_lock;
        static bool main_thread_exiting;

        bool panel1_draw_highlight = false;
        bool panel2_draw_highlight = false;
        bool panel3_draw_highlight = false;
        bool panel4_draw_highlight = false;

        Point mouse_position;

        bool application_dirty_state = false;

        public string seconds_to_readable(int time_in_seconds)
        {
            int hours = time_in_seconds / 3600;
            int minutes = (time_in_seconds % 3600) / 60;
            int seconds = (time_in_seconds % 3600) % 60;

            return (hours.ToString("00") + ":" + minutes.ToString("00") + ":" + seconds.ToString("00"));

        }

        public void set_load_id(string id)
        {
            load_id = id;
        }

        public void set_new_scientist_mouse_length(string scientist, string mouse, int xp_length_index)
        {
            new_scientist = scientist;
            new_mouse = mouse;
            new_xp_length_multiplier = xp_length_index + 1;
        }

        public void set_dialog_result(bool success)
        {
            last_dialog_success = success;
        }

        private void graceful_cleanup()
        {

            lock (main_thread_lock)
            {
                main_thread_exiting = true;
            }
            lock (thread1_lock) { }
            lock (thread2_lock) { }

            if (get_state(1) == xp_states.PROGRESSING)
            {
                set_state(1, xp_states.PAUSED);

                if(get_elasped_time(1) != 0)
                    label29.Text = "Experiment is Paused.";
                else
                    label29.Text = "Experiment is Ready.";

            }
            xp1_offset_time = get_elasped_time(1);
            lock (xp1_to_active_lock)
            {
                xp1_to_active = 0;
            }

            close_experiment(1);

            if (get_state(2) == xp_states.PROGRESSING)
            {
                set_state(2, xp_states.PAUSED);
                if (get_elasped_time(2) != 0)
                    label30.Text = "Experiment is Paused.";
                else
                    label30.Text = "Experiment is Ready.";
            }
            xp2_offset_time = get_elasped_time(2);
            lock (xp2_to_active_lock)
            {
                xp2_to_active = 0;
            }

            close_experiment(2);
        }

        private string generate_unique_hash()
        {

            string fullHash, hashKey;
            byte[] data;
            StringBuilder sBuilder;
            bool is_new;
            string sql;


            while (true)

            {
                using (MD5 md5Hash = MD5.Create())
                {
                    data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(DateTime.Now.ToString()));

                    sBuilder = new StringBuilder();

                    for (int i = 0; i < data.Length; i++)
                    {
                        sBuilder.Append(data[i].ToString("x2"));
                    }

                    fullHash = sBuilder.ToString();
                    hashKey = fullHash.Substring(0, 5);
                }

                is_new = true;

                sql = "select count(*) from experiment where id = '" + hashKey + "'";

                if (int.Parse(sql_helper.ExecuteScalar(sql)) != 0)
                    is_new = false;

                if (is_new == true)
                    return hashKey;
            }
        }


        private xp_states get_state(int xp_num)
        {
            if (xp_num == 1)
            {
                lock (xp1_state_lock)
                {
                    return xp1_state;
                }
            }

            else
            {
                lock (xp2_state_lock)
                {
                    return xp2_state;
                }
            }
        }


        private void set_state(int xp_num, xp_states state)
        {
            if (xp_num == 1)
            {
                lock (xp1_state_lock)
                {
                    xp1_state = state;
                }
            }

            else
            {
                lock (xp2_state_lock)
                {
                    xp2_state = state;
                }
            }
        }

        private int get_elasped_time(int xp_num)
        {
            double elapsed_time = 0;

            if (xp_num == 1)
            {
                if (get_state(1) == xp_states.DONE || get_state(1) == xp_states.PAUSED || get_state(1) == xp_states.NOTINITIALIZED)
                    return (int)xp1_offset_time;
                elapsed_time = (xp1_offset_time + DateTime.Now.Subtract(xp1_start_time).TotalSeconds);

                if (elapsed_time > const_3600 * xp1_length_multiplier)
                    elapsed_time = const_3600 * xp1_length_multiplier;
            }
            else
            {

                if (get_state(2) == xp_states.DONE || get_state(2) == xp_states.PAUSED || get_state(2) == xp_states.NOTINITIALIZED)
                    return (int)xp2_offset_time;
                elapsed_time = (xp2_offset_time + DateTime.Now.Subtract(xp2_start_time).TotalSeconds);

                if (elapsed_time > const_3600 * xp2_length_multiplier)
                    elapsed_time = const_3600 * xp2_length_multiplier;
            }

            return (int)elapsed_time;
        }



        private void update_xp1_1s()
        {
            lock (thread1_lock)
            {
                while (true)
                {

                    System.Threading.Thread.Sleep(1 * 1000);

                    lock (main_thread_lock)
                    {
                        if (main_thread_exiting == true)
                            break;
                    }

                    if (get_state(1) != xp_states.PROGRESSING)
                        break;

                    lock (xp1_to_active_lock)
                    {
                        if (xp1_to_active != 0)
                            xp1_to_active--;
                    }

                    int elapsed_time = get_elasped_time(1);

                    if (elapsed_time == const_3600 * xp1_length_multiplier)
                    {
                        set_state(1, xp_states.DONE);

                        store_experiment(1);

                        Invoke(elapsed_time_update_delegates[0], new object[] { elapsed_time });


                        Invoke(auto_unlock_delegates[0]);


                        if (checkBox1.Checked == true)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                System.Media.SystemSounds.Exclamation.Play();
                                System.Threading.Thread.Sleep(2 * 1000);
                            }
                            Invoke(message_box_delegate, new object[] { "Experiment 1 has just ended! Ask your mouse if he/she feels a little maniac-y.", "Experiment 1 Done" });
                            //MessageBox.Show(this, "Experiment 1 has just ended! Ask your mouse if he/she feels a little maniac-y.", "Experiment 1 Done");
                        }

                        break;
                    }

                    Invoke(elapsed_time_update_delegates[0], new object[] { elapsed_time });

                    if (elapsed_time % 10 == 0)
                    {
                        Invoke(panel_invalidate_delegates[0]);
                        Invoke(panel_invalidate_delegates[1]);
                        Invoke(panel_invalidate_delegates[4]);
                    }
                }
            }
        }

        private void update_xp2_1s()
        {
            lock (thread2_lock)
            {
                while (true)
                {
                    System.Threading.Thread.Sleep(1 * 1000);

                    lock (main_thread_lock)
                    {
                        if (main_thread_exiting == true)
                            break;
                    }

                    if (get_state(2) != xp_states.PROGRESSING)
                        break;

                    lock (xp2_to_active_lock)
                    {
                        if (xp2_to_active != 0)
                            xp2_to_active--;
                    }

                    int elapsed_time = get_elasped_time(2);

                    if (elapsed_time == const_3600 * xp2_length_multiplier)
                    {
                        set_state(2, xp_states.DONE);

                        store_experiment(2);

                        Invoke(elapsed_time_update_delegates[1], new object[] { elapsed_time });


                        Invoke(auto_unlock_delegates[1]);


                        if (checkBox2.Checked == true)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                System.Media.SystemSounds.Exclamation.Play();
                                System.Threading.Thread.Sleep(2 * 1000);
                            }

                            Invoke(message_box_delegate, new object[] { "Experiment 2 has just ended! Ask your mouse if he/she feels a little maniac-y.", "Experiment 2 Done" } );
                            //MessageBox.Show(this, "Experiment 2 has just ended! Ask your mouse if he/she feels a little maniac-y.", "Experiment 2 Done");
                        }
                        break;
                    }

                    Invoke(elapsed_time_update_delegates[1], new object[] { elapsed_time });

                    if (elapsed_time % 10 == 0)
                    {
                        Invoke(panel_invalidate_delegates[2]);
                        Invoke(panel_invalidate_delegates[3]);
                        Invoke(panel_invalidate_delegates[5]);

                    }
                }
            }
        }

        private void close_experiment(int which, bool store = true, bool create_new = false)
        {
            if (which == 1)
            {

                if (get_state(1) == xp_states.PROGRESSING)
                    pause_experiment(1);
                if (store == true)
                    store_experiment(1);

                if (create_new == true)
                {
                    button1.Enabled = false;
                    button2.Enabled = false;
                    checkBox3.Enabled = true;
                    button5.Enabled = true;
                    checkBox1.Enabled = true;
                    button6.Enabled = true;
                    button7.Enabled = true;
                    textBox3.ReadOnly = true;
                    //label25.ReadOnly = true;
                    //label26.ReadOnly = true;

                    xp1_id = generate_unique_hash();

                    set_state(1, xp_states.PAUSED);
                    if (get_elasped_time(1) != 0)
                        label29.Text = "Experiment is Paused.";
                    else
                        label29.Text = "Experiment is Ready.";


                    textBox3.Text = xp1_id;

                    new createpopup(xp1_id).ShowDialog();

                    //MessageBox.Show("To access the results of your experiment in the future you have to write down and keep the following code:\n\n" + xp1_id + "\n\nIn case you happen to lose this code, you may contact the person with admin rights.", "Experiment ID");

                    button11.Enabled = true;


                    xp1_scientist = new_scientist;
                    label25.Text = xp1_scientist;
                    xp1_mouse = new_mouse;
                    label26.Text = xp1_mouse;
                    xp1_length_multiplier = new_xp_length_multiplier;
                }

                else
                {
                    button1.Enabled = true;
                    button2.Enabled = true;
                    button5.Enabled = false;
                    checkBox1.Enabled = false;
                    button6.Enabled = false;
                    checkBox3.Enabled = false;
                    if (checkBox3.Checked != false)
                    {
                        checkBox3_programmatically_checked_change = true;
                        checkBox3.Checked = false;
                    }
                    button7.Enabled = false;
                    //textBox3.ReadOnly = false;
                    //label25.ReadOnly = false;
                    //label26.ReadOnly = false;

                    textBox3.Text = "";
                    label25.Text = "";
                    label26.Text = "";

                    label14.Text = "00:00:00";

                    label17.Text = "0(0)";
                    label19.Text = "0";

                    xp1_id = null;
                    set_state(1, xp_states.NOTINITIALIZED);
                    label29.Text = "Create or Load an Experiment to Start.";

                    button11.Enabled = false;


                }

                xp1_offset_time = 0;
                lock (xp1_to_active_lock)
                {
                    xp1_to_active = 0;
                }

                lock (xp1_data_lock[0])
                {
                    lock (xp1_data_lock[1])
                    {
                        for (int i = 0; i < 360; i++)
                            xp1_data.actives[i] = xp1_data.passives[i] = 0;
                    }
                }
                xp1_actives_total = xp1_passives_total = xp1_effective_actives_total = 0;

            }
            else
            {


                if (get_state(2) == xp_states.PROGRESSING)
                    pause_experiment(2);

                if (store == true)
                    store_experiment(2);

                if (create_new == true)
                {
                    button3.Enabled = false;
                    button4.Enabled = false;
                    checkBox4.Enabled = true;
                    button8.Enabled = true;
                    checkBox2.Enabled = true;
                    button9.Enabled = true;
                    button10.Enabled = true;
                    textBox4.ReadOnly = true;
                    //label27.ReadOnly = true;
                    //label28.ReadOnly = true;

                    xp2_id = generate_unique_hash();
                    set_state(2, xp_states.PAUSED);
                    if (get_elasped_time(2) != 0)
                        label30.Text = "Experiment is Paused.";
                    else
                        label30.Text = "Experiment is Ready.";

                    textBox4.Text = xp2_id;

                    new createpopup(xp2_id).ShowDialog();

                    //MessageBox.Show("To access the results of your experiment in the future you have to write down and keep the following code:\n\n" + xp2_id + "\n\nIn case you happen to lose this code, you may contact the person with admin rights.", "Experiment ID");

                    button12.Enabled = true;

                    xp2_scientist = new_scientist;
                    label27.Text = xp2_scientist;
                    xp2_mouse = new_mouse;
                    label28.Text = xp2_mouse;
                    xp2_length_multiplier = new_xp_length_multiplier;
                }

                else
                {
                    button3.Enabled = true;
                    button4.Enabled = true;
                    checkBox4.Enabled = false;
                    if (checkBox4.Checked != false)
                    {
                        checkBox4_programmatically_checked_change = true;
                        checkBox4.Checked = false;
                    }
                    button8.Enabled = false;
                    checkBox2.Enabled = false;
                    button9.Enabled = false;
                    button10.Enabled = false;
                    //textBox4.ReadOnly = false;
                    //label27.ReadOnly = false;
                    //label28.ReadOnly = false;

                    textBox4.Text = "";
                    label27.Text = "";
                    label28.Text = "";

                    label15.Text = "00:00:00";

                    label21.Text = "0(0)";
                    label23.Text = "0";

                    xp2_id = null;
                    set_state(2, xp_states.NOTINITIALIZED);
                    label30.Text = "Create or Load an Experiment to Start.";

                    button12.Enabled = false;

                }

                xp2_offset_time = 0;
                lock (xp2_to_active_lock)
                {
                    xp2_to_active = 0;
                }

                lock (xp2_data_lock[0])
                {
                    lock (xp2_data_lock[1])
                    {
                        for (int i = 0; i < 360; i++)
                            xp2_data.actives[i] = xp2_data.passives[i] = 0;
                    }
                }
                xp2_actives_total = xp2_passives_total = xp2_effective_actives_total = 0;


            }
        }

        private void pause_experiment(int which_xp)
        {
            if (which_xp == 1)
            {

                xp1_offset_time = get_elasped_time(1);
                lock (xp1_to_active_lock)
                {
                    xp1_to_active = 0;
                }

                set_state(1, xp_states.PAUSED);
                if(get_elasped_time(1) != 0)
                    label29.Text = "Experiment is Paused.";
                else
                    label29.Text = "Experiment is Ready.";


                button5.Text = "Continue";

            }
            else
            {

                xp2_offset_time = get_elasped_time(2);
                lock (xp2_to_active_lock)
                {
                    xp2_to_active = 0;
                }

                set_state(2, xp_states.PAUSED);
                if (get_elasped_time(2) != 0)
                    label30.Text = "Experiment is Paused.";
                else
                    label30.Text = "Experiment is Ready.";

                button8.Text = "Continue";

            }
        }

        private void play_experiment(int which_xp)
        {
            if (which_xp == 1)
            {
                button5.Text = "Pause";
                Thread xp1_thread = new Thread(update_xp1_1s);

                xp1_thread.IsBackground = true;

                xp1_start_time = new DateTime(DateTime.Now.Ticks);
                xp1_thread.Start();

                set_state(1, xp_states.PROGRESSING);
                label29.Text = "Experiment in Progress..";
            }
            else
            {
                button8.Text = "Pause";
                Thread xp2_thread = new Thread(update_xp2_1s);

                xp2_thread.IsBackground = true;

                xp2_start_time = new DateTime(DateTime.Now.Ticks);
                xp2_thread.Start();
                set_state(2, xp_states.PROGRESSING);
                label30.Text = "Experiment in Progress..";
            }
        }

        private void store_experiment(int which_xp)
        {
            string sql;

            if (which_xp == 1)
            {
                lock (xp1_data_lock[0])
                {
                    lock (xp1_data_lock[1])
                    {

                        sql = "UPDATE experiment SET scientist = '" + xp1_scientist + "', mouse = '" + xp1_mouse + "', actives = '" + string.Join(",", xp1_data.actives) + "', passives = '" + string.Join(",", xp1_data.passives) + "', elapsedtime = '" + get_elasped_time(1) + "', lengthmultiplier = '" + xp1_length_multiplier + "', state = '" + get_state(1).ToString() + "', totalactives = '" + xp1_actives_total.ToString() + "', totalpassives = '" + xp1_passives_total + "', totaleffectiveactives = '" + xp1_effective_actives_total.ToString() + "' WHERE id = '" + xp1_id + "'";

                    }
                }
                sql_helper.ExecuteNonQuery(sql);
            }
            else
            {
                lock (xp2_data_lock[0])
                {
                    lock (xp2_data_lock[1])
                    {
                        sql = "UPDATE experiment SET scientist = '" + xp2_scientist + "', mouse = '" + xp2_mouse + "', actives = '" + string.Join(",", xp2_data.actives) + "', passives = '" + string.Join(",", xp2_data.passives) + "', elapsedtime = '" + get_elasped_time(2) + "', lengthmultiplier = '" + xp2_length_multiplier + "', state = '" + get_state(2).ToString() + "', totalactives = '" + xp2_actives_total.ToString() + "', totalpassives = '" + xp2_passives_total + "', totaleffectiveactives = '" + xp2_effective_actives_total.ToString() + "' WHERE id = '" + xp2_id + "'";
                    }
                }
                sql_helper.ExecuteNonQuery(sql);
            }
        }

        private void load_experiment(int which_xp)
        {
            string id;
            string sql;

            if (which_xp == 1)
            {
                id = load_id;
                textBox3.Text = id;

                if (id == xp2_id && (get_state(2) == xp_states.PAUSED || get_state(2) == xp_states.PROGRESSING))
                {
                    MessageBox.Show(this, "An experiment of the same id is already in progress!", "Load Error");
                    return;
                }

                sql = "select count(*) from experiment where id = '" + id + "'";
                if (sql_helper.ExecuteScalar(sql).Equals("0"))
                {
                    MessageBox.Show(this, "No matching experiment of the given id was found!", "Load Error");
                    return;
                }

                textBox3.ReadOnly = true;
                //label25.ReadOnly = true;
                //label26.ReadOnly = true;

                sql = "select id from experiment where id = '" + id + "'";
                xp1_id = sql_helper.ExecuteScalar(sql);

                sql = "select elapsedtime from experiment where id = '" + id + "'";
                xp1_offset_time = double.Parse(sql_helper.ExecuteScalar(sql));
                lock (xp1_to_active_lock)
                {
                    xp1_to_active = 0;
                }

                sql = "select lengthmultiplier from experiment where id = '" + id + "'";
                xp1_length_multiplier = int.Parse(sql_helper.ExecuteScalar(sql));

                sql = "select totalactives from experiment where id = '" + id + "'";
                xp1_actives_total = int.Parse(sql_helper.ExecuteScalar(sql));

                sql = "select totalpassives from experiment where id = '" + id + "'";
                xp1_passives_total = int.Parse(sql_helper.ExecuteScalar(sql));

                sql = "select totaleffectiveactives from experiment where id = '" + id + "'";
                xp1_effective_actives_total = int.Parse(sql_helper.ExecuteScalar(sql));

                sql = "select scientist from experiment where id = '" + id + "'";
                xp1_scientist = sql_helper.ExecuteScalar(sql);

                sql = "select mouse from experiment where id = '" + id + "'";
                xp1_mouse = sql_helper.ExecuteScalar(sql);

                sql = "select state from experiment where id = '" + id + "'";
                if (sql_helper.ExecuteScalar(sql).Equals("DONE"))
                {
                    button5.Enabled = false;
                    checkBox1.Enabled = false;
                    button6.Enabled = false;
                    checkBox3.Enabled = false;
                    if (checkBox3.Checked != false)
                    {
                        checkBox3_programmatically_checked_change = true;
                        checkBox3.Checked = false;
                    }
                    button7.Enabled = true;

                    set_state(1, xp_states.DONE);
                    label29.Text = "Experiment is Finished.";


                }
                else
                {
                    button5.Enabled = true;
                    checkBox1.Enabled = true;
                    button6.Enabled = true;
                    checkBox3.Enabled = true;
                    button7.Enabled = true;

                    set_state(1, xp_states.PAUSED);
                    if (get_elasped_time(1) != 0)
                        label29.Text = "Experiment is Paused.";
                    else
                        label29.Text = "Experiment is Ready.";
                }

                button1.Enabled = false;
                button2.Enabled = false;
                //checkBox3.Enabled = true;

                sql = "select actives from experiment where id = '" + id + "'";
                lock (xp1_data_lock[0])
                {
                    xp1_data.actives = Array.ConvertAll(sql_helper.ExecuteScalar(sql).Split(','), int.Parse);
                }
                sql = "select passives from experiment where id = '" + id + "'";
                lock (xp1_data_lock[1])
                {
                    xp1_data.passives = Array.ConvertAll(sql_helper.ExecuteScalar(sql).Split(','), int.Parse);
                }
                textBox3.Text = xp1_id;
                label25.Text = xp1_scientist;
                label26.Text = xp1_mouse;

                label14.Text = seconds_to_readable((int)xp1_offset_time);
                label17.Text = xp1_actives_total.ToString() + "(" + xp1_effective_actives_total + ")";
                label19.Text = xp1_passives_total.ToString();

                button11.Enabled = true;

            }
            else
            {
                id = load_id;
                textBox4.Text = id;

                if (id == xp1_id && (get_state(1) == xp_states.PAUSED || get_state(1) == xp_states.PROGRESSING))
                {
                    MessageBox.Show(this, "An experiment of the same id is already in progress!", "Load Error");
                    return;
                }


                sql = "select count(*) from experiment where id = '" + id + "'";
                if (sql_helper.ExecuteScalar(sql).Equals("0"))
                {
                    MessageBox.Show(this, "No matching experiment of the given id was found!", "Load Error");
                    return;
                }

                textBox4.ReadOnly = true;
                //label27.ReadOnly = true;
                //label28.ReadOnly = true;

                sql = "select id from experiment where id = '" + id + "'";
                xp2_id = sql_helper.ExecuteScalar(sql);

                sql = "select elapsedtime from experiment where id = '" + id + "'";
                xp2_offset_time = double.Parse(sql_helper.ExecuteScalar(sql));
                lock (xp2_to_active_lock)
                {
                    xp2_to_active = 0;
                }
                sql = "select lengthmultiplier from experiment where id = '" + id + "'";
                xp2_length_multiplier = int.Parse(sql_helper.ExecuteScalar(sql));

                sql = "select totalactives from experiment where id = '" + id + "'";
                xp2_actives_total = int.Parse(sql_helper.ExecuteScalar(sql));

                sql = "select totalpassives from experiment where id = '" + id + "'";
                xp2_passives_total = int.Parse(sql_helper.ExecuteScalar(sql));

                sql = "select totaleffectiveactives from experiment where id = '" + id + "'";
                xp2_effective_actives_total = int.Parse(sql_helper.ExecuteScalar(sql));

                sql = "select scientist from experiment where id = '" + id + "'";
                xp2_scientist = sql_helper.ExecuteScalar(sql);

                sql = "select mouse from experiment where id = '" + id + "'";
                xp2_mouse = sql_helper.ExecuteScalar(sql);

                sql = "select state from experiment where id = '" + id + "'";
                if (sql_helper.ExecuteScalar(sql).Equals("DONE"))
                {
                    button8.Enabled = false;
                    checkBox2.Enabled = false;
                    button9.Enabled = false;
                    checkBox4.Enabled = false;
                    if (checkBox4.Checked != false)
                    {
                        checkBox4_programmatically_checked_change = true;
                        checkBox4.Checked = false;
                    }
                    button10.Enabled = true;
                    set_state(2, xp_states.DONE);
                    label30.Text = "Experiment is Finished.";

                }
                else
                {
                    button8.Enabled = true;
                    checkBox2.Enabled = true;
                    button9.Enabled = true;
                    checkBox4.Enabled = true;
                    button10.Enabled = true;
                    set_state(2, xp_states.PAUSED);
                    if (get_elasped_time(2) != 0)
                        label30.Text = "Experiment is Paused.";
                    else
                        label30.Text = "Experiment is Ready.";
                }

                button3.Enabled = false;
                button4.Enabled = false;
                //checkBox4.Enabled = true;

                sql = "select actives from experiment where id = '" + id + "'";
                lock (xp2_data_lock[0])
                {
                    xp2_data.actives = Array.ConvertAll(sql_helper.ExecuteScalar(sql).Split(','), int.Parse);
                }
                sql = "select passives from experiment where id = '" + id + "'";
                lock (xp2_data_lock[1])
                {
                    xp2_data.passives = Array.ConvertAll(sql_helper.ExecuteScalar(sql).Split(','), int.Parse);
                }
                textBox4.Text = xp2_id;
                label27.Text = xp2_scientist;
                label28.Text = xp2_mouse;

                label15.Text = seconds_to_readable((int)xp2_offset_time);
                label21.Text = xp2_actives_total.ToString() + "(" + xp2_effective_actives_total + ")";
                label23.Text = xp2_passives_total.ToString();


                button12.Enabled = true;

            }
        }
        public Form1()
        {
            InitializeComponent();

            this.Icon = Properties.Resources.maniouse_icon;

            panel_invalidate_delegates[0] = new PanelInvalidateCallback(delegatedPanel1Invalidate);
            panel_invalidate_delegates[1] = new PanelInvalidateCallback(delegatedPanel2Invalidate);
            panel_invalidate_delegates[2] = new PanelInvalidateCallback(delegatedPanel3Invalidate);
            panel_invalidate_delegates[3] = new PanelInvalidateCallback(delegatedPanel4Invalidate);
            panel_invalidate_delegates[4] = new PanelInvalidateCallback(delegatedPanel7Invalidate);
            panel_invalidate_delegates[5] = new PanelInvalidateCallback(delegatedPanel8Invalidate);

            auto_unlock_delegates[0] = new AutoUnlockCallback(delegatedAutoUnlock1);
            auto_unlock_delegates[1] = new AutoUnlockCallback(delegatedAutoUnlock2);

            elapsed_time_update_delegates[0] = new ElapsedTimeUpdateCallback(delegatedElapsedTime1Update);
            elapsed_time_update_delegates[1] = new ElapsedTimeUpdateCallback(delegatedElapsedTime2Update);

            message_box_delegate = new MessageBoxCallback(delegatedMessageBox);

            application_exit_delegate = new ApplicationExitCallback(delegatedApplicationExit);


            xp1_data.actives = new int[360];
            xp1_data.passives = new int[360];

            xp2_data.actives = new int[360];
            xp2_data.passives = new int[360];

            xp1_length_multiplier = xp2_length_multiplier = 3;

            checkBox3_programmatically_checked_change = checkBox4_programmatically_checked_change = false;

            xp1_data_lock[0] = "lock[0]".ToString();
            xp1_data_lock[1] = "lock[1]".ToString();
            xp2_data_lock[0] = "lock[2]".ToString();
            xp2_data_lock[1] = "lock[3]".ToString();
            xp1_state_lock = "lock[4]".ToString();
            xp2_state_lock = "lock[5]".ToString();
            thread1_lock = "lock[6]".ToString();
            thread2_lock = "lock[7]".ToString();
            main_thread_lock = "lock[8]".ToString();
            xp1_to_active_lock = "lock[9]".ToString();
            xp2_to_active_lock = "lock[10]".ToString();

            main_thread_exiting = false;

            close_experiment(1, false);
            close_experiment(2, false);

            for (int i = 0; i < 4; i++)
                xp_data_update_lock[i] = new Object();

            string db_dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDoc‌​uments), "Maniouse");
            string db_file = "maniouse.s3db";
            string path = System.IO.Path.Combine(db_dir, db_file);

            sql_helper = new SqliteHelper(db_dir, db_file);

            if (!sql_helper.TestConnection())
            {
                MessageBox.Show(this, "Database Connection Failed!", "Internal Error");
                application_dirty_state = true;
                Application.Exit();
            }

            Thread JoystickPollingThread = new Thread(JoystickPolling);

            JoystickPollingThread.IsBackground = true;
            JoystickPollingThread.Start();
        }

        delegate void AutoUnlockCallback();
        private void delegatedAutoUnlock1()
        {
            if (checkBox3.Checked == true)
            {
                button1.Visible = button2.Visible = button5.Visible = button6.Visible = button7.Visible = button11.Visible = true;
                checkBox1.Visible = true;
                //label29.Visible = false;
                if (checkBox3.Checked != false)
                {
                    checkBox3_programmatically_checked_change = true;
                    checkBox3.Checked = false;
                    label29.Text = "Experiment is Finished.";

                }
            }
            button5.Enabled = button6.Enabled = checkBox1.Enabled = checkBox3.Enabled = false;
        }
        private void delegatedAutoUnlock2()
        {
            if (checkBox4.Checked == true)
            {
                button3.Visible = button4.Visible = button8.Visible = button9.Visible = button10.Visible = button12.Visible = true;
                checkBox2.Visible = true;
                //label30.Visible = false;
                if (checkBox4.Checked != false)
                {
                    checkBox4_programmatically_checked_change = true;
                    checkBox4.Checked = false;
                    label30.Text = "Experiment is Finished.";

                }

            }
            button8.Enabled = button9.Enabled = checkBox2.Enabled = checkBox4.Enabled = false;
        }


        delegate void ElapsedTimeUpdateCallback(int elapsed_time);
        private void delegatedElapsedTime1Update(int elapsed_time)
        {

            label14.Text = seconds_to_readable((int)elapsed_time);
            label17.Text = xp1_actives_total.ToString() + "(" + xp1_effective_actives_total + ")";
            label19.Text = xp1_passives_total.ToString();
            if (checkBox3.Checked == false)
            {
                if (get_state(1) == xp_states.DONE)
                    label29.Text = "Experiment is Finished.";
            }
        }
        private void delegatedElapsedTime2Update(int elapsed_time)
        {

            label15.Text = seconds_to_readable((int)elapsed_time);
            label21.Text = xp2_actives_total.ToString() + "(" + xp2_effective_actives_total + ")";
            label23.Text = xp2_passives_total.ToString();
            if (checkBox3.Checked == false)
            {
                if (get_state(2) == xp_states.DONE)
                    label30.Text = "Experiment is Finished.";
            }
        }


        delegate void PanelInvalidateCallback();
        private void delegatedPanel1Invalidate()
        {
            panel1.Invalidate();
        }

        private void delegatedPanel2Invalidate()
        {
            panel2.Invalidate();
        }

        private void delegatedPanel3Invalidate()
        {
            panel3.Invalidate();
        }

        private void delegatedPanel4Invalidate()
        {
            panel4.Invalidate();
        }

        private void delegatedPanel7Invalidate()
        {
            panel7.Invalidate();
        }

        private void delegatedPanel8Invalidate()
        {
            panel8.Invalidate();
        }

        PanelInvalidateCallback[] panel_invalidate_delegates = new PanelInvalidateCallback[6];
        ElapsedTimeUpdateCallback[] elapsed_time_update_delegates = new ElapsedTimeUpdateCallback[2];
        AutoUnlockCallback[] auto_unlock_delegates = new AutoUnlockCallback[2];
        MessageBoxCallback message_box_delegate;

        delegate void MessageBoxCallback(string message, string title);
        private void delegatedMessageBox(string message, string title)
        {
            MessageBox.Show(this, message, title);
        }

        delegate void ApplicationExitCallback();
        private void delegatedApplicationExit()
        {
            Application.Exit();
        }

        ApplicationExitCallback application_exit_delegate;

        private void JoystickPolling()
        {
            try
            {

                int elapsed_seconds;
                int xp1_after_active, xp2_after_active;

                // Initialize DirectInput
                var directInput = new DirectInput();

                // Find a Joystick Guid
                var joystickGuid = Guid.Empty;

                foreach (var deviceInstance in directInput.GetDevices(DeviceType.Gamepad,
                            DeviceEnumerationFlags.AllDevices))
                    joystickGuid = deviceInstance.InstanceGuid;

                // If Gamepad not found, look for a Joystick
                if (joystickGuid == Guid.Empty)
                    foreach (var deviceInstance in directInput.GetDevices(DeviceType.Joystick,
                            DeviceEnumerationFlags.AllDevices))
                        joystickGuid = deviceInstance.InstanceGuid;

                // If Joystick not found, throws an error
                if (joystickGuid == Guid.Empty)
                {
                    Invoke(message_box_delegate, new object[] { "Problems receiving data from buttons!", "Device Not Found!" });
                    //MessageBox.Show(this, "Problems receiving data from buttons!", "Device Not Found!");
                    application_dirty_state = true;
                    Invoke(application_exit_delegate);
                }

                // Instantiate the joystick
                var joystick = new Joystick(directInput, joystickGuid);

                //MessageBox.Show("Found Joystick/Gamepad with GUID: {" + joystickGuid + "}");

                // Query all suported ForceFeedback effects
                var allEffects = joystick.GetEffects();
                //foreach (var effectInfo in allEffects)
                //    MessageBox.Show(this, "Effect available {" + effectInfo.Name + "}");

                // Set BufferSize in order to use buffered data.
                joystick.Properties.BufferSize = 128;

                // Acquire the joystick
                joystick.Acquire();

                // Poll events from joystick
                while (true)
                {

                    joystick.Poll();

                    var datas = joystick.GetBufferedData();

                    foreach (var state in datas)
                    {
                        if (state.Offset == JoystickOffset.Buttons0 && state.Value > 0)
                        {

                            if (get_state(1) == xp_states.PROGRESSING)
                            {
                                elapsed_seconds = get_elasped_time(1);
                            }
                            else
                                continue;

                            lock (xp1_data_lock[0])
                            {
                                lock(xp1_to_active_lock)
                                {
                                    if (xp1_to_active != 0)
                                        xp1_after_active = 30 - xp1_to_active;
                                    else
                                        xp1_after_active = 0;
                                }

                                if (xp1_data.actives[(elapsed_seconds - xp1_after_active) / 30]++ == 0)
                                {
                                    xp1_effective_actives_total++;
                                    xp1_to_active = 30;
                                }

                                xp1_actives_total++;

                                store_experiment(1);

                            }
                            Invoke(panel_invalidate_delegates[0]);

                        }

                        else
                        if (state.Offset == JoystickOffset.Buttons1 && state.Value > 0)
                        {

                            if (get_state(1) == xp_states.PROGRESSING)
                            {
                                elapsed_seconds = get_elasped_time(1);
                            }
                            else
                                continue;


                            lock (xp1_data_lock[1])
                            {
                                xp1_data.passives[elapsed_seconds / 30]++;
                                xp1_passives_total++;
                                store_experiment(1);

                            }
                            Invoke(panel_invalidate_delegates[1]);

                        }

                        else
                        if (state.Offset == JoystickOffset.Buttons2 && state.Value > 0)
                        {

                            if (get_state(2) == xp_states.PROGRESSING)
                            {
                                elapsed_seconds = get_elasped_time(2);
                            }
                            else
                                continue;


                            lock (xp2_data_lock[0])
                            {
                                lock (xp2_to_active_lock)
                                {
                                    if (xp2_to_active != 0)
                                        xp2_after_active = 30 - xp2_to_active;
                                    else
                                        xp2_after_active = 0;
                                }

                                if (xp2_data.actives[(elapsed_seconds - xp2_after_active) / 30]++ == 0)
                                {
                                    xp2_effective_actives_total++;
                                    xp2_to_active = 30;

                                }

                                xp2_actives_total++;

                                store_experiment(2);

                            }
                            Invoke(panel_invalidate_delegates[2]);

                        }

                        else
                        if (state.Offset == JoystickOffset.Buttons3 && state.Value > 0)
                        {

                            if (get_state(2) == xp_states.PROGRESSING)
                            {
                                elapsed_seconds = get_elasped_time(2);
                            }
                            else
                                continue;


                            lock (xp2_data_lock[1])
                            {
                                xp2_data.passives[elapsed_seconds / 30]++;
                                xp2_passives_total++;
                                store_experiment(2);

                            }
                            Invoke(panel_invalidate_delegates[3]);

                        }
                    }


                }
            }
            catch (Exception e)
            {
                Invoke(message_box_delegate, new object[] { "Problems receiving data from buttons!", "Device Not Found!" });
                //MessageBox.Show(this, "Problems receiving data from buttons!", "Internal Error");
                application_dirty_state = true;
                Invoke(application_exit_delegate);

            }
        }


        private void panel1_Paint(object sender, PaintEventArgs e)
        {
            Pen skyblue_pen = new Pen(Color.SkyBlue, 1);
            Pen orange_pen = new Pen(Color.Orange, 1);

            Pen highlight_pen = new Pen(Color.Yellow, 1);

            int X1 = 0, Y1 = 0, Y2 = 50;

            lock (xp1_data_lock[0])
            {
                for (int i = 0; i < 360; i++)
                {
                    if (xp1_data.actives[i] > 0)
                    {
                        if (xp1_data.actives[i] == 1)
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

            lock (xp1_data_lock[1])
            {
                for (int i = 0; i < 360; i++)
                    if (xp1_data.passives[i] > 0)
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
            }
            red_pen.Dispose();
            highlight_pen.Dispose();
        }

        private void panel3_Paint(object sender, PaintEventArgs e)
        {
            Pen skyblue_pen = new Pen(Color.SkyBlue, 1);
            Pen orange_pen = new Pen(Color.Orange, 1);

            Pen highlight_pen = new Pen(Color.Yellow, 1);

            int X1 = 0, Y1 = 0, Y2 = 50;

            lock (xp2_data_lock[0])
            {
                for (int i = 0; i < 360; i++)
                {
                    if (xp2_data.actives[i] > 0)
                    {
                        if (xp2_data.actives[i] == 1)
                            e.Graphics.DrawLine(skyblue_pen, X1 + i, Y1, X1 + i, Y2);
                        else
                            e.Graphics.DrawLine(orange_pen, X1 + i, Y1, X1 + i, Y2);
                    }
                }

                if (panel3_draw_highlight == true)
                {
                    int i;
                    do { i = mouse_position.X; } while (i < 0 || i > 359);


                    e.Graphics.DrawLine(highlight_pen, i, Y1, i, Y2);
                }
                else if (panel4_draw_highlight == false)
                {
                    label11.Text = "";
                    label12.Text = "";
                }
            }
            skyblue_pen.Dispose();
            orange_pen.Dispose();
            highlight_pen.Dispose();
        }

        private void panel4_Paint(object sender, PaintEventArgs e)
        {
            Pen red_pen = new Pen(Color.Red, 1);
            Pen highlight_pen = new Pen(Color.Yellow, 1);

            int X1 = 0, Y1 = 0, Y2 = 50;

            lock (xp2_data_lock[1])
            {
                for (int i = 0; i < 360; i++)
                    if (xp2_data.passives[i] > 0)
                        e.Graphics.DrawLine(red_pen, X1 + i, Y1, X1 + i, Y2);

                if (panel4_draw_highlight == true)
                {
                    int i;
                    do { i = mouse_position.X; } while (i < 0 || i > 359);


                    e.Graphics.DrawLine(highlight_pen, i, Y1, i, Y2);
                }
                else if (panel3_draw_highlight == false)
                {
                    label11.Text = "";
                    label12.Text = "";
                }
            }
            red_pen.Dispose();
            highlight_pen.Dispose();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            new create(this, sql_helper).ShowDialog();

            if (!last_dialog_success)
                return;

            close_experiment(1, false, true);

            button5.Text = "Start";

            string sql;
            lock (xp1_data_lock[0])
            {
                lock (xp1_data_lock[1])
                {
                    sql = "insert into experiment (id, scientist, mouse, actives, passives, elapsedtime, lengthmultiplier, totalactives, totalpassives, totaleffectiveactives, state) values ('" + xp1_id + "', '" + xp1_scientist + "', '" + xp1_mouse + "', '" + string.Join(",", xp1_data.actives) + "', '" + string.Join(",", xp1_data.passives) + "', '" + "0" + "', '" + xp1_length_multiplier.ToString() + "', '" + xp1_actives_total + "', '" + xp1_passives_total + "', '" + xp1_effective_actives_total + "', '" + "PAUSED" + "')";
                }
            }
            sql_helper.ExecuteNonQuery(sql);

            panel1.Invalidate();
            panel2.Invalidate();
            panel7.Invalidate();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            new load(this).ShowDialog();

            if (!last_dialog_success)
                return;


            load_experiment(1);

            if (xp1_offset_time != 0)
                button5.Text = "Continue";

            panel1.Invalidate();
            panel2.Invalidate();
            panel7.Invalidate();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            new create(this, sql_helper).ShowDialog();

            if (!last_dialog_success)
                return;

            close_experiment(2, false, true);

            button8.Text = "Start";
            string sql;
            lock (xp2_data_lock[0])
            {
                lock (xp2_data_lock[1])
                {
                    sql = "insert into experiment (id, scientist, mouse, actives, passives, elapsedtime, lengthmultiplier, totalactives, totalpassives, totaleffectiveactives, state) values ('" + xp2_id + "', '" + xp2_scientist + "', '" + xp2_mouse + "', '" + string.Join(",", xp2_data.actives) + "', '" + string.Join(",", xp2_data.passives) + "', '" + "0" + "', '" + xp2_length_multiplier.ToString() + "', '" + xp2_actives_total + "', '" + xp2_passives_total + "', '" + xp2_effective_actives_total + "', '" + "PAUSED" + "')";
                }
            }
            sql_helper.ExecuteNonQuery(sql);

            panel3.Invalidate();
            panel4.Invalidate();
            panel8.Invalidate();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            new load(this).ShowDialog();

            if (!last_dialog_success)
                return;

            load_experiment(2);

            if (xp2_offset_time != 0)
                button8.Text = "Continue";

            panel3.Invalidate();
            panel4.Invalidate();
            panel8.Invalidate();
        }

        private void button5_Click(object sender, EventArgs e)
        {

            if (get_state(1) == xp_states.PAUSED)
            {

                play_experiment(1);
            }
            else
            {
                switch (MessageBox.Show(this, "Are you sure you want to pause the ongoing experiment?", "Pausing Experiment", MessageBoxButtons.YesNo))
                {
                    case DialogResult.No:

                        return;

                    default:

                        pause_experiment(1);
                        store_experiment(1);

                        break;
                }
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {

            switch (MessageBox.Show(this, "Do you really want to end this experiment right now?", "Ending Experiment", MessageBoxButtons.YesNo))
            {
                case DialogResult.No:

                    return;

                default:

                    button5.Enabled = false;
                    checkBox1.Enabled = false;
                    button6.Enabled = false;
                    checkBox3.Enabled = false;
                    if (checkBox3.Checked != false)
                    {
                        checkBox3_programmatically_checked_change = true;
                        checkBox3.Checked = false;
                    }
                    button7.Enabled = true;


                    pause_experiment(1);
                    set_state(1, xp_states.DONE);
                    label29.Text = "Experiment is Finished.";


                    store_experiment(1);

                    break;
            }


        }

        private void button7_Click(object sender, EventArgs e)
        {
            string message;


            if (get_state(1) == xp_states.PROGRESSING)
                message = "This will pause and close the ongoing experiment. Are you sure?";

            else
                message = "Close the experiment? Make sure you have your reference ID written down.";



            switch (MessageBox.Show(this, message, "Closing Experiment", MessageBoxButtons.YesNo))
            {
                case DialogResult.No:

                    return;

                default:

                    close_experiment(1);
                    panel1.Invalidate();
                    panel2.Invalidate();
                    panel7.Invalidate();

                    break;
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            if (get_state(2) == xp_states.PAUSED)
            {
                play_experiment(2);
            }
            else
            {
                switch (MessageBox.Show(this, "Are you sure you want to pause the ongoing experiment?", "Pausing Experiment", MessageBoxButtons.YesNo))
                {
                    case DialogResult.No:

                        return;

                    default:

                        pause_experiment(2);
                        store_experiment(2);

                        break;
                }
            }
        }

        private void button9_Click(object sender, EventArgs e)
        {

            switch (MessageBox.Show(this, "Do you really want to end this experiment right now?", "Ending Experiment", MessageBoxButtons.YesNo))
            {
                case DialogResult.No:

                    return;

                default:

                    button8.Enabled = false;
                    checkBox2.Enabled = false;
                    button9.Enabled = false;
                    checkBox4.Enabled = false;
                    if (checkBox4.Checked != false)
                    {
                        checkBox4_programmatically_checked_change = true;
                        checkBox4.Checked = false;
                    }
                    button10.Enabled = true;


                    pause_experiment(2);
                    set_state(2, xp_states.DONE);
                    label30.Text = "Experiment is Finished.";


                    store_experiment(2);

                    break;
            }



        }

        private void button10_Click(object sender, EventArgs e)
        {
            string message;


            if (get_state(2) == xp_states.PROGRESSING)
                message = "This will pause and close the ongoing experiment. Are you sure?";

            else
                message = "Close the experiment? Make sure you have your reference ID written down.";


            switch (MessageBox.Show(this, message, "Closing Experiment", MessageBoxButtons.YesNo))
            {
                case DialogResult.No:

                    return;

                default:

                    close_experiment(2);
                    panel3.Invalidate();
                    panel4.Invalidate();
                    panel8.Invalidate();

                    break;
            }
        }

        private void panel1_MouseHover(object sender, EventArgs e)
        {

        }

        private void panel2_MouseHover(object sender, EventArgs e)
        {

        }

        private void panel3_MouseHover(object sender, EventArgs e)
        {

        }

        private void panel4_MouseHover(object sender, EventArgs e)
        {

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

        private void panel3_MouseLeave(object sender, EventArgs e)
        {
            panel3_draw_highlight = false;
            panel3.Invalidate();
        }

        private void panel4_MouseLeave(object sender, EventArgs e)
        {
            panel4_draw_highlight = false;
            panel4.Invalidate();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (application_dirty_state == true)
                return;

            switch (MessageBox.Show(this, "Are you sure you want to close?", "Exit Confirmation", MessageBoxButtons.YesNo))
            {
                case DialogResult.No:

                    e.Cancel = true;
                    break;

                default:

                    graceful_cleanup();
                    break;
            }
        }

        private void panel1_MouseMove(object sender, MouseEventArgs e)
        {
            mouse_position = panel1.PointToClient(Control.MousePosition);
            int i;
            do { i = mouse_position.X; } while (i < 0 || i > 359);


            label9.Text = seconds_to_readable(i * 30) + "-" + seconds_to_readable((i + 1) * 30);
            //label9.Text = (i * 30).ToString() + "-" + ((i + 1) * 30).ToString() + " Seconds";
            lock (xp1_data_lock[0])
            {
                label10.Text = xp1_data.actives[i].ToString() + " Presses";
            }

            panel1.Invalidate();
        }

        private void panel2_MouseMove(object sender, MouseEventArgs e)
        {
            mouse_position = panel2.PointToClient(Control.MousePosition);
            int i;
            do { i = mouse_position.X; } while (i < 0 || i > 359);


            label9.Text = seconds_to_readable(i * 30) + "-" + seconds_to_readable((i + 1) * 30);
            lock (xp1_data_lock[1])
            {
                label10.Text = xp1_data.passives[i].ToString() + " Presses";
            }

            panel2.Invalidate();
        }

        private void panel3_MouseMove(object sender, MouseEventArgs e)
        {
            mouse_position = panel3.PointToClient(Control.MousePosition);
            int i;
            do { i = mouse_position.X; } while (i < 0 || i > 359);


            label11.Text = seconds_to_readable(i * 30) + "-" + seconds_to_readable((i + 1) * 30);
            lock (xp2_data_lock[0])
            {
                label12.Text = xp2_data.actives[i].ToString() + " Presses";
            }

            panel3.Invalidate();
        }

        private void panel4_MouseMove(object sender, MouseEventArgs e)
        {
            mouse_position = panel4.PointToClient(Control.MousePosition);
            int i;
            do { i = mouse_position.X; } while (i < 0 || i > 359);


            label11.Text = seconds_to_readable(i * 30) + "-" + seconds_to_readable((i + 1) * 30);
            lock (xp2_data_lock[1])
            {
                label12.Text = xp2_data.passives[i].ToString() + " Presses";
            }

            panel4.Invalidate();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Hide();
            new adminpass(this, sql_helper).Show();
        }

        private void button11_Click(object sender, EventArgs e)
        {
            //DataTable dt_onerow;

            if (get_state(1) == xp_states.NOTINITIALIZED)
                return;

            store_experiment(1);

            string id = xp1_id;

            new exportprefs(sql_helper, id).ShowDialog();


            //string sql = "select count(*) from experiment where id = '" + id + "'";

            //if (sql_helper.ExecuteScalar(sql).Equals("0"))
            //    return;

            //sql = "select * from experiment where id='" + id + "'";
            //dt_onerow = sql_helper.GetDataTable(sql);
            //string sourcedir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Maniouse");
            //string inputFile = "Export-" + DateTime.Now.Ticks + ".csv";

            //string sourceFile = Path.Combine(sourcedir, inputFile);

            //if (!File.Exists(sourceFile))
            //{
            //    if (!Directory.Exists(sourcedir))
            //        Directory.CreateDirectory(sourcedir);
            //}
            //else
            //    return;

            //StringBuilder sb = new StringBuilder();

            //sb.Append("id,scientist,mouse,");

            //for (int i = 0; i < 360; i++)
            //    sb.Append((i * 30).ToString() + "-" + ((i + 1) * 30).ToString() + "-Actives,");
            //for (int i = 0; i < 360; i++)
            //    sb.Append((i * 30).ToString() + "-" + ((i + 1) * 30).ToString() + "-Passives,");

            //sb.AppendLine("elapsedtime,state");

            //foreach (DataRow row in dt_onerow.Rows)
            //{
            //    string[] fields = row.ItemArray.Select(field => field.ToString()).
            //                                    ToArray();
            //    sb.AppendLine(string.Join(",", fields));
            //}

            //File.WriteAllText(sourceFile, sb.ToString());

            //MessageBox.Show("File " + inputFile + " was successfully written to \"" + sourcedir + "\"");

        }

        private void button12_Click(object sender, EventArgs e)
        {

            if (get_state(2) == xp_states.NOTINITIALIZED)
                return;

            store_experiment(2);

            string id = xp2_id;

            new exportprefs(sql_helper, id).ShowDialog();




            //DataTable dt_onerow;

            //if (get_state(2) == xp_states.NOTINITIALIZED)
            //    return;

            //store_experiment(2);

            //string id = xp2_id;

            //string sql = "select count(*) from experiment where id = '" + id + "'";

            //if (sql_helper.ExecuteScalar(sql).Equals("0"))
            //    return;

            //sql = "select * from experiment where id='" + id + "'";
            //dt_onerow = sql_helper.GetDataTable(sql);
            //string sourcedir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Maniouse");
            //string inputFile = "Export-" + DateTime.Now.Ticks + ".csv";

            //string sourceFile = Path.Combine(sourcedir, inputFile);

            //if (!File.Exists(sourceFile))
            //{
            //    if (!Directory.Exists(sourcedir))
            //        Directory.CreateDirectory(sourcedir);
            //}
            //else
            //    return;

            //StringBuilder sb = new StringBuilder();

            //sb.Append("id,scientist,mouse,");

            //for (int i = 0; i < 360; i++)
            //    sb.Append((i * 30).ToString() + "-" + ((i + 1) * 30).ToString() + "-Actives,");
            //for (int i = 0; i < 360; i++)
            //    sb.Append((i * 30).ToString() + "-" + ((i + 1) * 30).ToString() + "-Passives,");

            //sb.AppendLine("elapsedtime,state");

            //foreach (DataRow row in dt_onerow.Rows)
            //{
            //    string[] fields = row.ItemArray.Select(field => field.ToString()).
            //                                    ToArray();
            //    sb.AppendLine(string.Join(",", fields));
            //}

            //File.WriteAllText(sourceFile, sb.ToString());

            //MessageBox.Show("File " + inputFile + " was successfully written to \"" + sourcedir + "\"");

        }

        private void panel1_MouseEnter(object sender, EventArgs e)
        {
            mouse_position = panel1.PointToClient(Control.MousePosition);
            int i;
            do { i = mouse_position.X; } while (i < 0 || i > 359);


            panel1_draw_highlight = true;
            label9.Text = seconds_to_readable(i * 30) + "-" + seconds_to_readable((i + 1) * 30);
            lock (xp1_data_lock[0])
            {
                label10.Text = xp1_data.actives[i].ToString() + " Presses";
            }

            panel1.Invalidate();
        }

        private void panel2_MouseEnter(object sender, EventArgs e)
        {
            mouse_position = panel2.PointToClient(Control.MousePosition);
            int i;
            do { i = mouse_position.X; } while (i < 0 || i > 359);


            panel2_draw_highlight = true;
            label9.Text = (i * 30).ToString() + "-" + ((i + 1) * 30).ToString() + " Seconds";
            lock (xp1_data_lock[1])
            {
                label10.Text = xp1_data.passives[i].ToString() + " Presses";
            }

            panel2.Invalidate();
        }

        private void panel3_MouseEnter(object sender, EventArgs e)
        {
            mouse_position = panel3.PointToClient(Control.MousePosition);
            int i;
            do { i = mouse_position.X; } while (i < 0 || i > 359);


            panel3_draw_highlight = true;
            label11.Text = seconds_to_readable(i * 30) + "-" + seconds_to_readable((i + 1) * 30);
            lock (xp2_data_lock[0])
            {
                label12.Text = xp2_data.actives[i].ToString() + " Presses";
            }

            panel3.Invalidate();
        }

        private void panel4_MouseEnter(object sender, EventArgs e)
        {
            mouse_position = panel4.PointToClient(Control.MousePosition);
            int i;
            do { i = mouse_position.X; } while (i < 0 || i > 359);

            panel4_draw_highlight = true;
            label11.Text = seconds_to_readable(i * 30) + "-" + seconds_to_readable((i + 1) * 30);
            lock (xp2_data_lock[1])
            {
                label12.Text = xp2_data.passives[i].ToString() + " Presses";
            }

            panel4.Invalidate();
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            new settings(sql_helper).ShowDialog();

        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox3_programmatically_checked_change == true)
            {
                checkBox3_programmatically_checked_change = false;
                return;
            }

            new lockdown(this, xp1_id, !(checkBox3.Checked)).ShowDialog();

            if (!last_dialog_success)
            {
                checkBox3_programmatically_checked_change = true;
                checkBox3.Checked = !(checkBox3.Checked);
                return;
            }

            if (checkBox3.Checked == true)
            {
                button1.Visible = button2.Visible = button5.Visible = button6.Visible = button7.Visible = button11.Visible = false;
                checkBox1.Visible = false;
                label29.Text = "Experiment is Locked!";
            }
            else
            {
                button1.Visible = button2.Visible = button5.Visible = button6.Visible = button7.Visible = button11.Visible = true;
                checkBox1.Visible = true;
                //label29.Visible = false;
            }

        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox4_programmatically_checked_change == true)
            {
                checkBox4_programmatically_checked_change = false;
                return;
            }

            new lockdown(this, xp2_id, !(checkBox4.Checked)).ShowDialog();

            if (!last_dialog_success)
            {
                checkBox4_programmatically_checked_change = true;
                checkBox4.Checked = !(checkBox4.Checked);
                return;
            }

            if (checkBox4.Checked == true)
            {
                button3.Visible = button4.Visible = button8.Visible = button9.Visible = button10.Visible = button12.Visible = false;
                checkBox2.Visible = false;
                label30.Text = "Experiment is Locked!";
            }
            else
            {
                button3.Visible = button4.Visible = button8.Visible = button9.Visible = button10.Visible = button12.Visible = true;
                checkBox2.Visible = true;
                //label30.Visible = false;
            }


        }

        private void panel7_Paint(object sender, PaintEventArgs e)
        {
            Pen black_pen = new Pen(Color.Black, 1);
            //Pen red_pen = new Pen(Color.Red, 1);

            int tp = (120 * xp1_length_multiplier) - 1;   //termination point
            int etid = get_elasped_time(1) / 30;    //elapsed_time_indicator_diversion

            int X1 = etid, Y1 = 0, Y2 = 200;

            Point[] DOWN = new Point[] { new Point(X1 - 7, 0), new Point(X1 + 7 + 1, 0), new Point(X1, 7) };
            Point[] UP = new Point[] { new Point(X1 - 7, 121), new Point(X1 + 7, 121), new Point(X1, 113) };

            //Point[] END_DOWN = new Point[] { new Point(tp - 7, 0), new Point(tp + 7, 0), new Point(tp, 7) };
            //Point[] END_UP = new Point[] { new Point(tp - 7, 121), new Point(tp + 7, 121), new Point(tp, 113) };

            e.Graphics.DrawLine(black_pen, X1, Y1, X1, Y2);


            if (get_state(1) != xp_states.NOTINITIALIZED)
                e.Graphics.DrawLine(black_pen, tp, Y1, tp, Y2);


            //e.Graphics.PixelOffsetMode = PixelOffsetMode.Default;
            using (SolidBrush brush = new SolidBrush(Color.Black))
            {
                e.Graphics.FillPolygon(brush, DOWN);
                e.Graphics.FillPolygon(brush, UP);
            }

            //using (SolidBrush brush = new SolidBrush(Color.Red))
            //{
            //    e.Graphics.FillPolygon(brush, END_DOWN);
            //    e.Graphics.FillPolygon(brush, END_UP);
            //}

            black_pen.Dispose();
        }

        private void panel8_Paint(object sender, PaintEventArgs e)
        {
            Pen black_pen = new Pen(Color.Black, 1);
            //Pen red_pen = new Pen(Color.Red, 1);

            int tp = (120 * xp2_length_multiplier) - 1;   //termination point
            int etid = get_elasped_time(2) / 30;    //elapsed_time_indicator_diversion

            int X1 = etid, Y1 = 0, Y2 = 200;

            Point[] DOWN = new Point[] { new Point(X1 - 7, 0), new Point(X1 + 7 + 1, 0), new Point(X1, 7) };
            Point[] UP = new Point[] { new Point(X1 - 7, 121), new Point(X1 + 7, 121), new Point(X1, 113) };

            //Point[] END_DOWN = new Point[] { new Point(tp - 7, 0), new Point(tp + 7, 0), new Point(tp, 7) };
            //Point[] END_UP = new Point[] { new Point(tp - 7, 121), new Point(tp + 7, 121), new Point(tp, 113) };

            e.Graphics.DrawLine(black_pen, X1, Y1, X1, Y2);


            if (get_state(2) != xp_states.NOTINITIALIZED)
                e.Graphics.DrawLine(black_pen, tp, Y1, tp, Y2);


            //e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            using (SolidBrush brush = new SolidBrush(Color.Black))
            {
                e.Graphics.FillPolygon(brush, DOWN);
                e.Graphics.FillPolygon(brush, UP);
            }

            //using (SolidBrush brush = new SolidBrush(Color.Red))
            //{
            //    e.Graphics.FillPolygon(brush, END_DOWN);
            //    e.Graphics.FillPolygon(brush, END_UP);
            //}

            black_pen.Dispose();
        }
    }

    public class SqliteHelper
    {
        protected String dbConnection;

        public SqliteHelper(string DBDirectoryInfo, String inputFile)
        {

            string sourceFile = Path.Combine(DBDirectoryInfo, inputFile);
            dbConnection = String.Format("Data Source={0}", sourceFile);

            if (!File.Exists(sourceFile))
            {
                if (!Directory.Exists(DBDirectoryInfo))
                    Directory.CreateDirectory(DBDirectoryInfo);

                SQLiteConnection.CreateFile(sourceFile);

                string sql;

                sql = "create table experiment (id char(5) NOT NULL PRIMARY KEY, scientist nvarchar(50), mouse nvarchar(50), actives text NOT NULL, passives text NOT NULL, elapsedtime varchar(50) NOT NULL, lengthmultiplier char(1) NOT NULL, totalactives varchar(10) NOT NULL, totalpassives varchar(10) NOT NULL, totaleffectiveactives varchar(10) NOT NULL, state varchar(20) NOT NULL)";
                this.ExecuteNonQuery(sql);

                sql = "create table application (adminpass nvarchar(50), defaultxplengthmultiplier varchar(10), defaultmultiplierforexportintervals char(1))";
                this.ExecuteNonQuery(sql);

                sql = "insert into application(adminpass, defaultxplengthmultiplier, defaultmultiplierforexportintervals) values('admin', '2', '2')";
                this.ExecuteNonQuery(sql);
            }
        }

        public DataTable GetDataTable(string sql)
        {
            DataTable dt = new DataTable();
            try
            {
                SQLiteConnection cnn = new SQLiteConnection(dbConnection);
                cnn.Open();
                SQLiteCommand mycommand = new SQLiteCommand(cnn);
                mycommand.CommandText = sql;
                SQLiteDataReader reader = mycommand.ExecuteReader();
                dt.Load(reader);
                reader.Close();
                cnn.Close();
            }
            catch (Exception ex)
            {
            }
            return dt;
        }

        public int ExecuteNonQuery(string sql)
        {
            SQLiteConnection cnn = new SQLiteConnection(dbConnection);
            cnn.Open();
            SQLiteCommand mycommand = new SQLiteCommand(cnn);
            mycommand.CommandText = sql;
            int rowsUpdated = mycommand.ExecuteNonQuery();
            cnn.Close();
            return rowsUpdated;
        }

        public string ExecuteScalar(string sql)
        {
            SQLiteConnection cnn = new SQLiteConnection(dbConnection);
            cnn.Open();
            SQLiteCommand mycommand = new SQLiteCommand(cnn);
            mycommand.CommandText = sql;
            object value = mycommand.ExecuteScalar();
            cnn.Close();
            if (value != null)
            {
                return value.ToString();
            }
            return "";
        }

        public bool TestConnection()
        {
            using (SQLiteConnection cnn = new SQLiteConnection(dbConnection))
            {
                try
                {
                    cnn.Open();
                    return true;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    if ((cnn != null) && (cnn.State != ConnectionState.Open))
                        cnn.Close();
                }
            }
        }
    }
}
