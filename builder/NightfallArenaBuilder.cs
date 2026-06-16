using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace NightfallArenaBuilder
{
    public class BuilderForm : Form
    {
        private readonly string root;
        private readonly string configPath;
        private readonly string configScriptPath;
        private readonly string gamePath;
        private readonly string gitPath;
        private TextBox editor;
        private TextBox status;
        private TreeView outline;

        public BuilderForm()
        {
            root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            configPath = Path.Combine(root, "game-config.json");
            configScriptPath = Path.Combine(root, "game-config.js");
            gamePath = Path.Combine(root, "index.html");
            gitPath = root;

            Text = "Nightfall Arena Builder";
            Width = 1180;
            Height = 760;
            MinimumSize = new Size(860, 560);
            StartPosition = FormStartPosition.CenterScreen;

            BuildUi();
            LoadConfig();
        }

        private void BuildUi()
        {
            var top = new FlowLayoutPanel();
            top.Dock = DockStyle.Top;
            top.Height = 46;
            top.Padding = new Padding(8, 8, 8, 6);
            top.BackColor = Color.FromArgb(28, 32, 36);

            AddButton(top, "Reload", LoadConfig);
            AddButton(top, "Validate", ValidateConfig);
            AddButton(top, "Save", SaveConfig);
            AddButton(top, "Open Game", OpenGame);
            AddButton(top, "Open Folder", OpenFolder);
            AddButton(top, "Push Pages", PushPages);

            var split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.SplitterDistance = 280;

            outline = new TreeView();
            outline.Dock = DockStyle.Fill;
            outline.Font = new Font("Segoe UI", 10);
            split.Panel1.Controls.Add(outline);

            editor = new TextBox();
            editor.AcceptsReturn = true;
            editor.AcceptsTab = true;
            editor.Dock = DockStyle.Fill;
            editor.Font = new Font("Consolas", 11);
            editor.Multiline = true;
            editor.ScrollBars = ScrollBars.Both;
            editor.WordWrap = false;
            split.Panel2.Controls.Add(editor);

            status = new TextBox();
            status.Dock = DockStyle.Bottom;
            status.Height = 110;
            status.Multiline = true;
            status.ReadOnly = true;
            status.ScrollBars = ScrollBars.Vertical;
            status.BackColor = Color.FromArgb(18, 20, 22);
            status.ForeColor = Color.WhiteSmoke;
            status.Font = new Font("Consolas", 9);

            Controls.Add(split);
            Controls.Add(status);
            Controls.Add(top);
        }

        private void AddButton(FlowLayoutPanel panel, string label, Action action)
        {
            var button = new Button();
            button.Text = label;
            button.Width = 112;
            button.Height = 30;
            button.Click += delegate { action(); };
            panel.Controls.Add(button);
        }

        private void LoadConfig()
        {
            if (!File.Exists(configPath))
            {
                Log("Missing game-config.json beside the EXE.");
                return;
            }

            editor.Text = File.ReadAllText(configPath, Encoding.UTF8);
            BuildOutline();
            Log("Loaded " + configPath);
        }

        private object ParseConfig()
        {
            var serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            return serializer.DeserializeObject(editor.Text);
        }

        private void ValidateConfig()
        {
            try
            {
                ParseConfig();
                BuildOutline();
                Log("Config is valid JSON.");
            }
            catch (Exception ex)
            {
                Log("JSON error: " + ex.Message);
            }
        }

        private void SaveConfig()
        {
            try
            {
                ParseConfig();
                File.WriteAllText(configPath, editor.Text, Encoding.UTF8);
                File.WriteAllText(configScriptPath, "window.NIGHTFALL_CONFIG = " + editor.Text.Trim() + ";" + Environment.NewLine, Encoding.UTF8);
                BuildOutline();
                Log("Saved game-config.json and game-config.js. Refresh the game page to see changes.");
            }
            catch (Exception ex)
            {
                Log("Not saved. Fix this JSON error first: " + ex.Message);
            }
        }

        private void BuildOutline()
        {
            outline.Nodes.Clear();
            try
            {
                var parsed = ParseConfig();
                var rootNode = new TreeNode("game-config.json");
                AddNodeChildren(rootNode, parsed);
                outline.Nodes.Add(rootNode);
                rootNode.Expand();
            }
            catch
            {
                outline.Nodes.Add("JSON has errors");
            }
        }

        private void AddNodeChildren(TreeNode parent, object value)
        {
            var dict = value as System.Collections.IDictionary;
            if (dict != null)
            {
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    var child = new TreeNode(Convert.ToString(entry.Key));
                    parent.Nodes.Add(child);
                    AddNodeChildren(child, entry.Value);
                }
                return;
            }

            var array = value as object[];
            if (array != null)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    var child = new TreeNode("[" + i + "]");
                    parent.Nodes.Add(child);
                    AddNodeChildren(child, array[i]);
                }
                return;
            }

            parent.Text = parent.Text + ": " + Convert.ToString(value);
        }

        private void OpenGame()
        {
            if (!File.Exists(gamePath))
            {
                Log("Missing index.html beside the EXE.");
                return;
            }
            Process.Start(gamePath);
        }

        private void OpenFolder()
        {
            Process.Start(root);
        }

        private void PushPages()
        {
            SaveConfig();
            RunGit("add index.html game.js game-config.json game-config.js");
            RunGit("commit -m \"Update Nightfall Arena config\"");
            RunGit("push");
        }

        private void RunGit(string args)
        {
            try
            {
                var start = new ProcessStartInfo("git", args);
                start.WorkingDirectory = gitPath;
                start.UseShellExecute = false;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.CreateNoWindow = true;
                var process = Process.Start(start);
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Log("> git " + args + Environment.NewLine + output + error);
            }
            catch (Exception ex)
            {
                Log("Git failed: " + ex.Message);
            }
        }

        private void Log(string message)
        {
            status.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BuilderForm());
        }
    }
}
