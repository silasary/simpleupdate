using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows.Forms;

namespace Updater  // Generic auto-updater.
{
    class Program
    {
        static WebClient client = new WebClient();

        static void Main(string[] args)
        {
            bool Generate = false;
            bool Elevated = false;
            if (args.Contains("-g"))
                Generate = true;
            if (args.Contains("-e"))
                Elevated = true;
            string[] manifests = Directory.GetFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "*.updatemanifest",SearchOption.AllDirectories);
            if (manifests.Length == 0)
            {
                try
                {
                    MessageBox.Show("Update Manifest not found.");
                }
                catch (TypeInitializationException)
                {
                    Console.WriteLine("Update Manifest not found.");
                }
                return;
            }
            
            for (int i = 0; i < manifests.Length; i++)
            {
                Environment.CurrentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string file = manifests[i];
                string[] manifest = File.ReadAllLines(file);
                Environment.CurrentDirectory = Path.GetDirectoryName(file);
                bool downloaded = false;
                int mode = 0;
                string address = "";
                for (int l = 0; l < manifest.Length; l++)
                {
                    string line = manifest[l];
                    if (line == "" || line[0] == '#')
                        continue;
                    string key = line.Split(':')[0];
                    string value = line.Substring(line.IndexOf(":") + 1);
                    switch (key.ToLowerInvariant())
                    {
                        case ("manifest"):
                            if (!Generate && !downloaded) // Download the latest version of the manifest, and start from the top.
                            {
                                try
                                {
                                    client.DownloadFile(value, Path.GetFileName(file));
                                }

                                catch (WebException v)
                                {
                                    if (v.InnerException is UnauthorizedAccessException && !Elevated)
                                    {
                                        #region Elevate
                                        ProcessStartInfo proc = new ProcessStartInfo();
                                        proc.UseShellExecute = true;
                                        proc.FileName = Assembly.GetEntryAssembly().Location;
                                        proc.Verb = "runas";
                                        proc.Arguments = "-e" + (Generate ? " -g" : "");
                                        try
                                        {
                                            Process.Start(proc);
                                            return;
                                        }
                                        catch
                                        {
                                            // The user refused the elevation.
                                            // We can't update, so let's just quit.
                                            return;
                                        }
                                        #endregion
                                    }
                                    else
                                    {
                                        Console.WriteLine(v.ToString());
                                        Console.ReadKey();
                                        return;
                                    }
                                }

                                downloaded = true;
                                l = 0;
                                manifest = File.ReadAllLines(Path.GetFileName(file));
                            }
                            break;
                        case ("file"):
                            file = value.Trim();
                            break;
                        case ("address"):
                            address = value.Trim();
                            break;
                        case ("md5"):
                            if (Generate)
                            {
                                manifest[l] = "md5:" + MD5File(file);
                                break;
                            }
                            if (value.Trim() == "")
                                break;
                            if (mode == 0 && MD5File(file) != value.Trim())
                            {
                                Console.WriteLine("Updating " + file + "...");
                                DownloadFile(address, file);
                            }
                            else if (mode == 1 && File.Exists(file) && MD5File(file) != value.Trim())
                            {
                                DownloadFile(address, file);
                            }
                            else if (mode == -1 && !File.Exists(file))
                            {
                                DownloadFile(address, file);
                            }
                            break;
                        case ("terminate"):
                            foreach (Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(file.Trim())))
                            {
                                Console.WriteLine("Please Close " + value);
                                p.CloseMainWindow();
                                p.WaitForExit(10000);
                                if (!p.HasExited)
                                {
                                    MessageBox.Show("Press OK to force " + p.MainWindowTitle + " to close.");
                                    if (!p.HasExited)
                                        p.Kill();
                                }
                            }
                            break;
                        case ("launch"):
                            if (!Generate)
                                Process.Start(value.Trim());
                            break;
                    }
                }
                if (Generate)
                    File.WriteAllLines(manifests[i], manifest);
            }
        }

        private static void DownloadFile(string address, string file)
        {
            try
            {
                client.DownloadFile(address, file);

            }
            catch (WebException v)
            {
                if (v.InnerException is UnauthorizedAccessException || v.InnerException is IOException)
                {
                    if (File.Exists(file + ".old"))
                        File.Delete(file + ".old");
                    File.Move(file, file + ".old");
                    client.DownloadFile(address, file);
                }
            }
        }

        public static string MD5File(string path)
        {
            if (!File.Exists(path))
                return "";
            Stream stream = File.OpenRead(path);
            try
            {
                System.Security.Cryptography.MD5CryptoServiceProvider cryptHandler;
                cryptHandler = new System.Security.Cryptography.MD5CryptoServiceProvider();
                byte[] hash = cryptHandler.ComputeHash(stream);
                string ret = "";
                foreach (byte a in hash)
                {
                    if (a < 16)
                        ret += "0" + a.ToString("x");
                    else
                        ret += a.ToString("x");
                }
                stream.Close();
                return ret;
            }
            catch
            {
                stream.Close();
                throw;
            }
        }

    }
}
