using System;
using System.IO;
using Microsoft.Win32;

namespace Devvio.Archiver.Core
{
    /// <summary>
    /// Locates a 7-Zip command line tool (7z.exe). The bundled copy shipped with the
    /// installer wins, then an installed 7-Zip, then anything on the PATH.
    /// </summary>
    public static class SevenZipLocator
    {
        private static readonly object Gate = new object();
        private static string cached;
        private static bool resolved;

        public static string FindSevenZipExe()
        {
            lock (Gate)
            {
                if (resolved)
                {
                    return cached;
                }
                resolved = true;
                cached = Search();
                return cached;
            }
        }

        private static string Search()
        {
            try
            {
                // 1. Bundled with the application (next to DevvioArchiver.Core.dll).
                string localDir = null;
                try
                {
                    localDir = Path.GetDirectoryName(typeof(SevenZipLocator).Assembly.Location);
                }
                catch
                {
                }
                if (!string.IsNullOrEmpty(localDir))
                {
                    string bundled = Path.Combine(localDir, "7z", "7z.exe");
                    if (File.Exists(bundled))
                    {
                        return bundled;
                    }
                    string flat = Path.Combine(localDir, "7z.exe");
                    if (File.Exists(flat))
                    {
                        return flat;
                    }
                }

                // 2. Installed 7-Zip (per-machine or per-user).
                foreach (RegistryKey root in new[] { Registry.LocalMachine, Registry.CurrentUser })
                {
                    foreach (string sub in new[] { @"SOFTWARE\7-Zip", @"SOFTWARE\WOW6432Node\7-Zip" })
                    {
                        try
                        {
                            using (RegistryKey key = root.OpenSubKey(sub))
                            {
                                if (key == null)
                                {
                                    continue;
                                }
                                string dir = key.GetValue("Path") as string;
                                if (string.IsNullOrEmpty(dir))
                                {
                                    continue;
                                }
                                string candidate = Path.Combine(dir.TrimEnd('\\'), "7z.exe");
                                if (File.Exists(candidate))
                                {
                                    return candidate;
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                // 3. Well-known folders.
                string[] folders =
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "7-Zip")
                };
                foreach (string folder in folders)
                {
                    if (string.IsNullOrEmpty(folder))
                    {
                        continue;
                    }
                    try
                    {
                        string candidate = Path.Combine(folder, "7z.exe");
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                    catch
                    {
                    }
                }

                // 4. PATH.
                try
                {
                    string pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                    foreach (string rawDir in pathVar.Split(';'))
                    {
                        if (string.IsNullOrWhiteSpace(rawDir))
                        {
                            continue;
                        }
                        try
                        {
                            string candidate = Path.Combine(rawDir.Trim(), "7z.exe");
                            if (File.Exists(candidate))
                            {
                                return candidate;
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            }
            catch
            {
            }
            return null;
        }
    }
}
