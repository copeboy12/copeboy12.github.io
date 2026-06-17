using System;
using System.Collections.Generic;
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
        private Label settingHelp;
        private ToolTip tips;

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
            tips = new ToolTip();
            tips.AutoPopDelay = 18000;
            tips.InitialDelay = 350;
            tips.ReshowDelay = 100;

            var header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 98;
            header.Padding = new Padding(10);
            header.BackColor = Color.FromArgb(244, 247, 250);

            var top = new FlowLayoutPanel();
            top.Dock = DockStyle.Top;
            top.Height = 42;
            top.BackColor = Color.FromArgb(244, 247, 250);

            AddButton(top, "Reload", "Reload game-config.json from disk.", LoadConfig);
            AddButton(top, "Validate", "Check whether the settings text is valid JSON before saving.", ValidateConfig);
            AddButton(top, "Save", "Save your changes into game-config.json and game-config.js.", SaveConfig);
            AddButton(top, "Open Game", "Open the editable browser version of the game.", OpenGame);
            AddButton(top, "Build Game EXE", "Create or update the playable NightfallArenaGame.exe.", BuildGameExe);
            AddButton(top, "Open Build", "Open the folder where the playable EXE gets created.", OpenBuildFolder);
            AddButton(top, "Open Folder", "Open the whole project folder.", OpenFolder);
            AddButton(top, "Push Pages", "Upload the web version to GitHub Pages.", PushPages);

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
            outline.ShowNodeToolTips = true;
            outline.AfterSelect += delegate { ShowSettingHelp(outline.SelectedNode); };

            settingHelp = new Label();
            settingHelp.Dock = DockStyle.Bottom;
            settingHelp.Height = 150;
            settingHelp.Padding = new Padding(10);
            settingHelp.BackColor = Color.FromArgb(235, 244, 255);
            settingHelp.ForeColor = Color.FromArgb(20, 35, 50);
            settingHelp.Font = new Font("Segoe UI", 11);
            settingHelp.Text = "Click any setting on the left to see what it does.";

            split.Panel1.Controls.Add(outline);
            split.Panel1.Controls.Add(settingHelp);

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

        private void AddButton(FlowLayoutPanel panel, string label, string help, Action action)
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
            tips.SetToolTip(button, help);
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
                rootNode.Tag = "";
                rootNode.ToolTipText = GetSettingHelp("");
                AddNodeChildren(rootNode, parsed, "");
                outline.Nodes.Add(rootNode);
                rootNode.Expand();
            }
            catch
            {
                outline.Nodes.Add("JSON has errors");
            }
        }

        private void AddNodeChildren(TreeNode parent, object value, string parentPath)
        {
            var dict = value as System.Collections.IDictionary;
            if (dict != null)
            {
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    string key = Convert.ToString(entry.Key);
                    string path = string.IsNullOrEmpty(parentPath) ? key : parentPath + "." + key;
                    var child = new TreeNode(key);
                    child.Tag = path;
                    child.ToolTipText = GetSettingHelp(path);
                    parent.Nodes.Add(child);
                    AddNodeChildren(child, entry.Value, path);
                }
                return;
            }

            var array = value as object[];
            if (array != null)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    string path = parentPath + "[" + i + "]";
                    var child = new TreeNode("[" + i + "]");
                    child.Tag = path;
                    child.ToolTipText = GetSettingHelp(parentPath);
                    parent.Nodes.Add(child);
                    AddNodeChildren(child, array[i], path);
                }
                return;
            }

            parent.Text = parent.Text + ": " + Convert.ToString(value);
        }

        private void ShowSettingHelp(TreeNode node)
        {
            if (node == null)
            {
                settingHelp.Text = "Click any setting on the left to see what it does.";
                return;
            }

            string path = Convert.ToString(node.Tag);
            settingHelp.Text = node.Text + Environment.NewLine + GetSettingHelp(path);
        }

        private string GetSettingHelp(string path)
        {
            var help = new Dictionary<string, string>();
            help[""] = "This is the full game settings file. Click a setting below to learn what it changes.";
            help["world"] = "World settings control how large the arena is.";
            help["world.width"] = "How wide the whole arena is in game units. Bigger means more room to run left and right.";
            help["world.height"] = "How tall the whole arena is in game units. Bigger means more room to run up and down.";
            help["projection"] = "2.5D camera settings. These control the tilted look and where the player appears on screen.";
            help["projection.tilt"] = "How flat or steep the 2.5D ground looks. Lower like 0.45 looks flatter. Higher like 0.75 looks less flat. Avoid huge values.";
            help["projection.anchorY"] = "Where the player sits vertically on the screen. Smaller number moves the player up. Bigger number moves the player down.";
            help["player"] = "Starting stats for the player character.";
            help["player.radius"] = "Player collision size. Bigger means enemies hit you from farther away.";
            help["player.speed"] = "How fast the player moves.";
            help["player.maxHp"] = "Starting and maximum health.";
            help["player.nextXp"] = "How much XP is needed for the first level-up.";
            help["player.attackCooldown"] = "Time between automatic attacks. Lower is faster.";
            help["player.shots"] = "How many projectiles you fire at the start.";
            help["player.damage"] = "Starting damage per projectile.";
            help["player.pickupRange"] = "How close XP gems need to be before they get pulled toward you.";
            help["wave"] = "Enemy wave and spawn pacing.";
            help["wave.secondsPerWave"] = "How many seconds before the game counts as the next harder wave.";
            help["wave.spawnBaseSeconds"] = "Base delay between enemy spawns at the start.";
            help["wave.spawnReductionPerWave"] = "How much faster spawning gets each wave.";
            help["wave.spawnMinimumSeconds"] = "Fastest allowed spawn delay. Keeps the game from spawning infinitely fast.";
            help["wave.baseSpawnCount"] = "How many enemies spawn each spawn cycle before wave bonuses.";
            help["wave.maxExtraSpawnCount"] = "Maximum extra enemies added from wave difficulty.";
            help["enemy"] = "Enemy size, health, damage, speed, XP, and spawn distance.";
            help["enemy.spawnDistanceMin"] = "Minimum distance enemies appear away from the player.";
            help["enemy.spawnDistanceRandom"] = "Extra random spawn distance added on top of the minimum.";
            help["enemy.bruteChanceBase"] = "Starting chance for a big enemy to spawn.";
            help["enemy.bruteChancePerWave"] = "How much the big enemy chance rises each wave.";
            help["enemy.bruteChanceMax"] = "Highest allowed chance for big enemies.";
            help["enemy.shadeRadius"] = "Small enemy collision/body size.";
            help["enemy.shadeHpBase"] = "Small enemy starting health.";
            help["enemy.shadeHpPerWave"] = "Extra small enemy health gained each wave.";
            help["enemy.shadeSpeedBase"] = "Small enemy starting speed.";
            help["enemy.shadeSpeedPerWave"] = "Extra small enemy speed gained each wave.";
            help["enemy.shadeDamage"] = "Small enemy damage per second while touching you.";
            help["enemy.shadeXp"] = "XP dropped by small enemies.";
            help["enemy.bruteRadius"] = "Big enemy collision/body size.";
            help["enemy.bruteHpBase"] = "Big enemy starting health.";
            help["enemy.bruteHpPerWave"] = "Extra big enemy health gained each wave.";
            help["enemy.bruteSpeedBase"] = "Big enemy starting speed.";
            help["enemy.bruteSpeedPerWave"] = "Extra big enemy speed gained each wave.";
            help["enemy.bruteDamage"] = "Big enemy damage per second while touching you.";
            help["enemy.bruteXp"] = "XP dropped by big enemies.";
            help["weapon"] = "Projectile behavior.";
            help["weapon.projectileSpeed"] = "How fast projectiles fly.";
            help["weapon.projectileRadius"] = "Projectile collision size. Bigger makes shots easier to hit.";
            help["weapon.projectileLife"] = "How long projectiles exist before disappearing.";
            help["weapon.multiShotSpread"] = "Angle between multiple shots. Bigger spreads them wider.";
            help["upgrades"] = "Values used by level-up upgrades.";
            help["upgrades.quickHandsMultiplier"] = "Attack cooldown multiplier from Quick Hands. Lower means a stronger speed upgrade.";
            help["upgrades.quickHandsMinimumCooldown"] = "Fastest attack cooldown Quick Hands can reach.";
            help["upgrades.sharpenedSteelMultiplier"] = "Damage multiplier from Sharpened Steel.";
            help["upgrades.longStepBonus"] = "Movement speed added by Long Step.";
            help["upgrades.magnetCharmBonus"] = "Pickup range added by Magnet Charm.";
            help["upgrades.ironHeartMaxHpBonus"] = "Max HP added by Iron Heart.";
            help["upgrades.ironHeartHeal"] = "Immediate healing from Iron Heart.";

            return help.ContainsKey(path) ? help[path] : "No custom explanation yet. This is still editable, but I have not written a plain-English tooltip for it.";
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
