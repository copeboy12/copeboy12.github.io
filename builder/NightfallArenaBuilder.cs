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
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10);

            BuildUi();
            LoadConfig();
        }

        private void BuildUi()
        {
            var header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 98;
            header.Padding = new Padding(10);
            header.BackColor = Color.FromArgb(244, 247, 250);

            var top = new FlowLayoutPanel();
            top.Dock = DockStyle.Top;
            top.Height = 42;
            top.BackColor = Color.FromArgb(244, 247, 250);

            AddButton(top, "Reload", LoadConfig);
            AddButton(top, "Validate", ValidateConfig);
            AddButton(top, "Save", SaveConfig);
            AddButton(top, "Open Game", OpenGame);
            AddButton(top, "Build Game EXE", BuildGameExe);
            AddButton(top, "Open Build", OpenBuildFolder);
            AddButton(top, "Open Folder", OpenFolder);
            AddButton(top, "Push Pages", PushPages);

            var help = new Label();
            help.Dock = DockStyle.Fill;
            help.Padding = new Padding(4, 8, 4, 0);
            help.Font = new Font("Segoe UI", 11, FontStyle.Regular);
            help.ForeColor = Color.FromArgb(35, 40, 45);
            help.Text = "Beginner mode: change values on the right, click Save, then click Build Game EXE. " +
                "Use Open Build to find the playable EXE. If you break the JSON, Validate will tell you before saving.";

            header.Controls.Add(help);
            header.Controls.Add(top);

            var split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.SplitterDistance = 340;

            outline = new TreeView();
            outline.Dock = DockStyle.Fill;
            outline.Font = new Font("Segoe UI", 12);
            outline.BackColor = Color.White;
            outline.ForeColor = Color.FromArgb(25, 30, 35);
            split.Panel1.Controls.Add(outline);

            editor = new TextBox();
            editor.AcceptsReturn = true;
            editor.AcceptsTab = true;
            editor.Dock = DockStyle.Fill;
            editor.Font = new Font("Consolas", 13);
            editor.Multiline = true;
            editor.ScrollBars = ScrollBars.Both;
            editor.WordWrap = false;
            editor.BackColor = Color.White;
            editor.ForeColor = Color.Black;
            split.Panel2.Controls.Add(editor);

            status = new TextBox();
            status.Dock = DockStyle.Bottom;
            status.Height = 130;
            status.Multiline = true;
            status.ReadOnly = true;
            status.ScrollBars = ScrollBars.Vertical;
            status.BackColor = Color.FromArgb(250, 252, 255);
            status.ForeColor = Color.FromArgb(25, 30, 35);
            status.Font = new Font("Consolas", 11);

            Controls.Add(split);
            Controls.Add(status);
            Controls.Add(header);
        }

        private void AddButton(FlowLayoutPanel panel, string label, Action action)
        {
            var button = new Button();
            button.Text = label;
            button.Width = 128;
            button.Height = 34;
            button.Margin = new Padding(0, 0, 8, 0);
            button.BackColor = Color.White;
            button.ForeColor = Color.Black;
            button.FlatStyle = FlatStyle.Standard;
            button.Font = new Font("Segoe UI", 9, FontStyle.Bold);
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

        private void OpenBuildFolder()
        {
            string buildRoot = Path.Combine(root, "dist", "NightfallArenaGame");
            Directory.CreateDirectory(buildRoot);
            Process.Start(buildRoot);
        }

        private void BuildGameExe()
        {
            SaveConfig();

            string buildRoot = Path.Combine(root, "dist", "NightfallArenaGame");
            string wwwRoot = Path.Combine(buildRoot, "www");
            Directory.CreateDirectory(wwwRoot);

            CopyFile("index.html", wwwRoot);
            CopyFile("style.css", wwwRoot);
            CopyFile("game.js", wwwRoot);
            CopyFile("game-config.json", wwwRoot);
            CopyFile("game-config.js", wwwRoot);
            CopyFolderIfExists("assets", Path.Combine(wwwRoot, "assets"));

            string csc = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe";
            if (!File.Exists(csc)) csc = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe";
            if (!File.Exists(csc))
            {
                Log("Could not find the Windows C# compiler, so I could not build the game EXE.");
                return;
            }

            string source = Path.Combine(root, "builder", "NightfallArenaGameLauncher.cs");
            string output = Path.Combine(buildRoot, "NightfallArenaGame.exe");
            RunProcess(csc, "/target:winexe /platform:anycpu /out:\"" + output + "\" /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll \"" + source + "\"", root);
            Log("Built game EXE here: " + output);
        }

        private void CopyFile(string fileName, string destinationFolder)
        {
            string source = Path.Combine(root, fileName);
            if (!File.Exists(source))
            {
                Log("Skipped missing file: " + fileName);
                return;
            }

            File.Copy(source, Path.Combine(destinationFolder, fileName), true);
        }

        private void CopyFolderIfExists(string folderName, string destination)
        {
            string source = Path.Combine(root, folderName);
            if (!Directory.Exists(source)) return;

            if (Directory.Exists(destination)) Directory.Delete(destination, true);
            Directory.CreateDirectory(destination);
            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(directory.Replace(source, destination));
            }
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                File.Copy(file, file.Replace(source, destination), true);
            }
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
            RunProcess("git", args, gitPath);
        }

        private void RunProcess(string fileName, string args, string workingDirectory)
        {
            try
            {
                var start = new ProcessStartInfo(fileName, args);
                start.WorkingDirectory = workingDirectory;
                start.UseShellExecute = false;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.CreateNoWindow = true;
                var process = Process.Start(start);
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Log("> " + fileName + " " + args + Environment.NewLine + output + error);
            }
            catch (Exception ex)
            {
                Log(fileName + " failed: " + ex.Message);
            }
        }

        private void Log(string message)
        {
            status.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        }

        [STAThread]
        public static void Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new BuilderForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Nightfall Arena Builder crashed");
            }
        }
    }
}
