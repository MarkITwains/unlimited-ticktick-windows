using System.Diagnostics;
using Microsoft.Win32;

namespace TTPatcher
{
    class Program
    {
        static int Main(string[] args)
        {
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
            Console.WriteLine("TTPatcher - TickTick License Patcher");
            Console.WriteLine("=====================================");

            // No arguments => one-click mode: locate installed TickTick, patch in place.
            // With an argument => patch the given file (original CLI behavior).
            if (args.Length == 0)
            {
                return RunOneClickMode();
            }
            return RunCliMode(args);
        }

        // ---------- One-click mode ----------

        static int RunOneClickMode()
        {
            Console.WriteLine("One-click mode: patching the installed TickTick in place.");
            Console.WriteLine();

            var tickTickPath = FindInstalledTickTick();
            if (tickTickPath == null)
            {
                Console.WriteLine("❌ Could not locate the installed TickTick.exe.");
                Console.WriteLine("   Looked in the registry (HKLM/HKCU uninstall keys) and the default");
                Console.WriteLine("   install locations. Reinstall TickTick, or run:");
                Console.WriteLine("     TTPatcher.exe \"C:\\path\\to\\TickTick.exe\"");
                return 1;
            }
            var info = FileVersionInfo.GetVersionInfo(tickTickPath);
            Console.WriteLine($"✅ Found TickTick {info.FileVersion} at: {tickTickPath}");
            Console.WriteLine();

            if (!EnsureWriteAccess(tickTickPath))
                return 1;

            var backupPath = CreateBackup(tickTickPath);
            if (backupPath == null)
                return 1;

            KillTickTickProcesses();

            var patcher = new DnlibAssemblyPatcher();
            var patchedTempPath = tickTickPath + ".patched.tmp";
            try
            {
                Console.WriteLine();
                if (!patcher.PatchAssembly(tickTickPath, patchedTempPath))
                {
                    Console.WriteLine("❌ Patching failed! Your original TickTick.exe was NOT modified.");
                    File.Delete(patchedTempPath);
                    return 1;
                }

                File.SetAttributes(tickTickPath, FileAttributes.Normal);
                File.Delete(tickTickPath);
                File.Move(patchedTempPath, tickTickPath);
                Console.WriteLine($"✅ Patched executable installed: {tickTickPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to replace the executable: {ex.Message}");
                Console.WriteLine("   Restoring the original exe from backup...");
                try
                {
                    if (File.Exists(patchedTempPath)) File.Delete(patchedTempPath);
                    if (File.Exists(tickTickPath)) File.Delete(tickTickPath);
                    File.Copy(backupPath, tickTickPath, overwrite: true);
                    Console.WriteLine($"✅ Original restored from: {backupPath}");
                }
                catch (Exception rex)
                {
                    Console.WriteLine($"⚠️ Automatic restore failed: {rex.Message}");
                    Console.WriteLine($"   Manually copy this backup over the exe: {backupPath}");
                }
                return 1;
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to launch TickTick (or close this window to skip)...");
            try { Console.ReadKey(intercept: true); } catch { }
            LaunchTickTick(tickTickPath);

            return 0;
        }

        static string? FindInstalledTickTick()
        {
            var candidates = new List<string>();

            // Registry uninstall keys are the most reliable source for the install dir.
            foreach (var root in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            {
                foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                {
                    using var baseKey = RegistryKey.OpenBaseKey(root, view);
                    foreach (var sub in new[]
                             {
                                 @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                                 @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                             })
                    {
                        using var uninstall = baseKey.OpenSubKey(sub);
                        if (uninstall == null) continue;
                        foreach (var keyName in uninstall.GetSubKeyNames())
                        {
                            using var entry = uninstall.OpenSubKey(keyName);
                            if (entry == null) continue;
                            var displayIcon = entry.GetValue("DisplayIcon") as string;
                            var installLocation = entry.GetValue("InstallLocation") as string;
                            var displayName = entry.GetValue("DisplayName") as string;
                            if ((displayName != null && displayName.IndexOf("TickTick", StringComparison.OrdinalIgnoreCase) >= 0)
                                || keyName.IndexOf("TickTick", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                if (!string.IsNullOrWhiteSpace(installLocation))
                                    candidates.Add(Path.Combine(installLocation.Trim(), "TickTick.exe"));
                                if (!string.IsNullOrWhiteSpace(displayIcon)
                                    && displayIcon.IndexOf("TickTick.exe", StringComparison.OrdinalIgnoreCase) >= 0)
                                    candidates.Add(displayIcon.Split(',')[0].Trim('"', ' '));
                            }
                        }
                    }
                }
            }

            // Fallbacks for installs the registry doesn't know about.
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "TickTick", "TickTick.exe"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "TickTick", "TickTick.exe"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TickTick", "TickTick.exe"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "TickTick", "TickTick.exe"));

            foreach (var c in candidates)
            {
                if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
                    return Path.GetFullPath(c);
            }
            return null;
        }

        static bool EnsureWriteAccess(string tickTickPath)
        {
            // Program Files writes need admin; the self-relaunch below handles the UAC prompt.
            // Test with a temp file in the install dir so we never touch the (possibly running) exe.
            var dir = Path.GetDirectoryName(tickTickPath)!;
            var testFile = Path.Combine(dir, Path.GetRandomFileName());
            try
            {
                File.WriteAllBytes(testFile, new byte[1]);
                File.Delete(testFile);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Administrator rights are required to modify files in the install folder.");
                Console.WriteLine("Requesting elevation...");
                try
                {
                    var exePath = Environment.ProcessPath;
                    if (exePath == null) { Console.WriteLine("❌ Cannot determine own path for elevation."); return false; }

                    var psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        Verb = "runas",
                        WorkingDirectory = Environment.CurrentDirectory
                    };
                    using var proc = Process.Start(psi);
                    if (proc == null) { Console.WriteLine("❌ Failed to relaunch elevated."); return false; }
                    proc.WaitForExit();
                    // The elevated child did the whole job; the parent must not patch again.
                    Environment.Exit(proc.ExitCode);
                    return false; // unreachable
                }
                catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
                {
                    Console.WriteLine("❌ Elevation was declined. Cannot patch the installed TickTick.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Cannot write to the install folder: {ex.Message}");
                return false;
            }
        }

        static string? CreateBackup(string tickTickPath)
        {
            try
            {
                var dir = Path.GetDirectoryName(tickTickPath)!;
                var name = Path.GetFileNameWithoutExtension(tickTickPath);
                var backupPath = Path.Combine(dir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.bak.exe");
                File.Copy(tickTickPath, backupPath, overwrite: true);
                Console.WriteLine($"✅ Backup created: {backupPath}");
                return backupPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to create backup: {ex.Message}");
                Console.WriteLine("   Aborting — refusing to patch without a backup.");
                return null;
            }
        }

        static void KillTickTickProcesses()
        {
            var killed = false;
            foreach (var p in Process.GetProcessesByName("TickTick"))
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(5000);
                    killed = true;
                    Console.WriteLine($"✅ Closed TickTick process (PID {p.Id})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Could not close TickTick process (PID {p.Id}): {ex.Message}");
                }
            }
            if (!killed) Console.WriteLine("TickTick is not running.");
        }

        static void LaunchTickTick(string tickTickPath)
        {
            try
            {
                using var _ = Process.Start(new ProcessStartInfo { FileName = tickTickPath, UseShellExecute = true });
                Console.WriteLine("✅ TickTick launched.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Could not launch TickTick: {ex.Message}");
            }
        }

        // ---------- CLI mode (original behavior) ----------

        static int RunCliMode(string[] args)
        {
            // Parse command line arguments
            var inputPath = GetInputPath(args);
            if (inputPath == null) return 1;

            // Generate output path
            var outputPath = GenerateOutputPath(inputPath);

            // Create patcher and run
            var patcher = new DnlibAssemblyPatcher();
            var success = patcher.PatchAssembly(inputPath, outputPath);

            if (success)
            {
                Console.WriteLine($"✅ Patching completed successfully!");
                Console.WriteLine($"📁 Patched file: {outputPath}");
                return 0;
            }
            return 1;
        }

        private static string? GetInputPath(string[] args)
        {
            string inputPath;

            // Check if path was provided as command line argument
            if (args.Length > 0)
            {
                inputPath = args[0].Trim('"'); // Remove quotes if present
                Console.WriteLine($"Using provided path: {inputPath}");
            }
            else
            {
                // Look for TickTick.exe in current directory
                inputPath = Path.Combine(Directory.GetCurrentDirectory(), "TickTick.exe");
                Console.WriteLine($"Looking for TickTick.exe in current directory: {inputPath}");
            }

            if (!File.Exists(inputPath))
            {
                if (args.Length > 0)
                {
                    Console.WriteLine("❌ File not found at the provided path!");
                }
                else
                {
                    Console.WriteLine("❌ TickTick.exe not found in the current directory!");
                    Console.WriteLine();
                    Console.WriteLine("Usage:");
                    Console.WriteLine("  TTPatcher.exe                           - One-click: patch the installed TickTick");
                    Console.WriteLine("  TTPatcher.exe \"path\\to\\TickTick.exe\"   - Patch a specific file");
                }
                return null;
            }

            Console.WriteLine($"✅ Found TickTick.exe at: {inputPath}");
            return inputPath;
        }

        private static string GenerateOutputPath(string inputPath)
        {
            var directory = Path.GetDirectoryName(inputPath) ?? "";
            var fileName = Path.GetFileNameWithoutExtension(inputPath);
            var extension = Path.GetExtension(inputPath);

            return Path.Combine(directory, $"{fileName}_Patched{extension}");
        }
    }
}
