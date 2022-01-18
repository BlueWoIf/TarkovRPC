using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Threading;
using IronOcr;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using DiscordRPC;
using DiscordRPC.Logging;

namespace TarkovRP
{
    class Program
    {
        public static DiscordRpcClient client;

        private static readonly string discordclientId = "931883249335152640";

        //private static States lastrpstate;
        private static States lastrpstate = States.None;

        private static readonly string projectPath = Directory.GetCurrentDirectory();

        private static string lastMap;

        private static readonly string[] maps = { "Factory", "Customs", "Reserve", "Shoreline", "Woods", "Lighthouse", "Interchange", "The Lab" };

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        static void Main()
        {
            //Installation.LicenseKey = "key"; - put your IronOCR License here, otherwise you can't run the project outside Visual Studio
            UsernameStuff();
            StartRPC();
            Checks();
        }
        static void Checks()
        {
            Console.Clear();
            Thread.Sleep(1000);
            Console.WriteLine("Initiating checks...");
            Process[] processlist = Process.GetProcesses();
            if (Array.Exists(processlist, x => x.ProcessName == "EscapeFromTarkov"))
            {
                if (GetActiveWindowTitle() == "EscapeFromTarkov")
                {
                    Console.WriteLine("Focused inside Tarkov");
                    //map name
                    ScreenshotByCords(849, 164, 178, 38, "map");
                    ScreenshotByCords(20, 1061, 542, 112, "spawndata");
                    ScreenshotByCords(9, 1169, 307, 26, "raiduserid");
                    OcrResult SpawnDataResult = new IronTesseract().Read($@"{projectPath}\spawndata.png");
                    OcrResult MapResult = new IronTesseract().Read($@"{projectPath}\map.png");
                    OcrResult RaidIdResult = new IronTesseract().Read($@"{ projectPath}\raiduserid.png");
                    if (Array.Exists(maps, x => x == MapResult.Text))
                    {
                        lastMap = MapResult.Text;
                        Console.WriteLine("Loading into Raid...");
                        UpdateRPC(null, "Loading into Raid", MapResult.Text.ToLower(), MapResult.Text, null, null, States.Loading, true);
                        Checks();
                    }
                    else if (RaidIdResult.Text.StartsWith("0") || RaidIdResult.Text.ToLower().StartsWith("o"))
                    {
                        if (SpawnDataResult.Text.ToLower().Contains("usec"))
                        {
                            Console.WriteLine("Spawned as a USEC operative");
                            UpdateRPC(null, "In Raid", lastMap.ToLower(), lastMap, "usec", "USEC", States.InRaid, true);
                            Checks();
                        }
                        else if (SpawnDataResult.Text.ToLower().Contains("bear"))
                        {
                            Console.WriteLine("Spawned as a BEAR operative");
                            UpdateRPC(null, "In Raid", lastMap.ToLower(), lastMap, "bear", "BEAR", States.InRaid, true);
                            Checks();
                        }
                        else if (SpawnDataResult.Text.ToLower().Contains("day"))
                        {
                            Console.WriteLine("Spawned as a SCAV");
                            UpdateRPC(null, "In Raid", lastMap.ToLower(), lastMap, "scav", "SCAV", States.InRaid, true);
                            Checks();
                        }
                        else
                        {
                            Checks();
                        }
                    }
                    else
                    {
                        Console.WriteLine("Not loading into match, returning...");
                        UpdateRPC(null, "In Menu", "menu", "Escape from Tarkov", null, null, States.Menu, false);
                        Thread.Sleep(1000);
                        Checks();
                    }
                }
                else
                {
                    Console.WriteLine("Not Focused inside Tarkov, returning...");
                    UpdateRPC(null, "-", null, null, null, null, lastrpstate, false);
                    Thread.Sleep(1000);
                    Checks();
                }
            }
            else
            {
                //no tarkov opened
                Console.WriteLine("Can't find Tarkov, returning...");
                client.ClearPresence();
                Checks();
            }
        }
        private static void UpdateRPC(string details, string state, string largeimagekey, string largeimagetext, string smallimagekey, string smallimagetext, States from, bool timestamp)
        {
            Console.WriteLine("Updating Rich Presence...");

            if (lastrpstate == from)
            {
                Console.WriteLine("Same state, not updated");
                return;
            }
            else
            {
                if(timestamp)
                {
                    client.SetPresence(new RichPresence()
                    {
                        Details = details,
                        State = state,
                        Timestamps = new Timestamps()
                        {
                            Start = DateTime.UtcNow,
                        },
                        Assets = new Assets()
                        {
                            LargeImageKey = largeimagekey,
                            LargeImageText = largeimagetext,
                            SmallImageKey = smallimagekey,
                            SmallImageText = smallimagetext,
                        }
                    });
                }
                else
                {
                    client.SetPresence(new RichPresence()
                    {
                        Details = details,
                        State = state,
                        Timestamps = new Timestamps()
                        {
                            Start = null,
                        },
                        Assets = new Assets()
                        {
                            LargeImageKey = largeimagekey,
                            LargeImageText = largeimagetext,
                        }
                    });
                }
                Console.WriteLine("Updated.");
                lastrpstate = from;
                return;
            }
        }
        public static void StartRPC()
        {
            client = new DiscordRpcClient(discordclientId);
            client.Logger = new ConsoleLogger() { Level = LogLevel.Warning };
            client.OnReady += (sender, e) =>
            {
                Console.WriteLine($"Displaying Rich Presence for: {e.User.Username}");
            };
            client.OnPresenceUpdate += (sender, e) =>
            {
                Console.WriteLine("Received Update! {0}", e.Presence);
            };

            client.Initialize();
            return;
        }
        static void UsernameStuff()
        {
            string usernameFile = $@"{projectPath}\username.txt";
            if (File.Exists(usernameFile))
            {
                string username = File.ReadAllText(usernameFile);
                Console.Write($"Signed in as {username}!");
                Console.ReadKey();
            }
            else
            {
                Console.Write("Hey! First time setup thing time! Your username: ");
                string username = Console.ReadLine();
                File.WriteAllText(usernameFile, username);
                Main();
            }
        }
        private static void ScreenshotByCords(int startx, int starty, int width, int height, string filename)
        {
            Rectangle rect = new Rectangle(startx, starty, width, height);
            Bitmap bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bitmap);
            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
            bitmap.Save($@"{projectPath}\{filename}.png", ImageFormat.Png);
        }
        private static string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }
    }
}
