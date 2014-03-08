using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Windows.Documents;

namespace SteamBigPictureLosingFocusFix
{
    public partial class App : Application
    {
        private const int WatcherStopTimeout = 2000;
        private const int WatcherPollTime = 1000;
        private const string SteamProcessName = "Steam";
        private const string BigPictureWindowTitle = "Steam";
        private const string BigPictureWindowClassName = "CUIEngineWin32";

        private readonly ConcurrentDictionary<int, Process> _steamChildProcesses = new ConcurrentDictionary<int, Process>();
        private readonly ManualResetEvent _watcherStopSignal = new ManualResetEvent(false);

        private ManagementEventWatcher _processMonitor;
        private Task _watcherTask;
        private Process _steamProcess;

        protected override void OnStartup(StartupEventArgs e)
        {
            Log("Program started");

            TryWatchSteamProcess();

            // If Steam is already running then start monitor Big Picture.
            if (IsSteamProcessRunnig()) StartFocusWatcher();

            StartProcessMonitor();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            bool isStopped = TryStopFocusWatcher();

            if (!isStopped)
            {
                Log("Error: Failed to stop focus watcher on exit.");
                Environment.Exit(0);
            }

            Log("Focus watcher has been stopped.");

            base.OnExit(e);
        }

        private static void Log(string text)
        {
            Debug.WriteLine("[{0}]: {1}", DateTime.Now.ToString("HH:mm:ss"), text);
        }

        private void TryWatchSteamProcess()
        {
            _steamProcess = Process.GetProcessesByName(SteamProcessName).FirstOrDefault();

            if (_steamProcess == null) return;

            _steamProcess.EnableRaisingEvents = true;
            _steamProcess.Exited += SteamProcessOnExited;

            Log("Started watching Steam process.");
        }

        private void SteamProcessOnExited(object sender, EventArgs eventArgs)
        {
            Log("Steam process exited.");

            _steamChildProcesses.Clear();

            if (IsFocusWatcherRunning())
            {
                bool isStopped = TryStopFocusWatcher();

                if (!isStopped)
                {
                    Log("Error: Failed to stop focus watcher.");
                    Environment.Exit(0);
                }

                Log("Focus watcher has been stopped.");
            }

            _steamProcess = null;
        }

        private bool IsSteamProcessRunnig()
        {
            return (_steamProcess != null);
        }

        private bool IsSteamProcess(Process process)
        {
            return (process.ProcessName == SteamProcessName);
        }

        private bool IsSteamChildProcessId(int id)
        {
            return 
                _steamProcess != null &&
                (_steamProcess.Id == id || _steamChildProcesses.ContainsKey(id));
        }

        private void StartFocusWatcher()
        {
            _watcherStopSignal.Reset();
            _watcherTask = Task.Factory.StartNew(FocusWatcher);

            Log("Focus watcher started.");
        }

        private bool TryStopFocusWatcher()
        {
            _watcherStopSignal.Set();

            return _watcherTask.Wait(WatcherStopTimeout);
        }

        private bool IsFocusWatcherRunning()
        {
            return (_watcherTask != null && !_watcherTask.Wait(0));
        }

        private bool DoesSteamHasChildProcesses()
        {
            return (_steamChildProcesses.Count > 0);
        }

        private void FocusWatcher()
        {
            while (true)
            {
                bool isStopping = _watcherStopSignal.WaitOne(WatcherPollTime);
                if (isStopping) return;

                try
                {
                    if (DoesSteamHasChildProcesses()) continue;

                    TrySwitchToBigPicture();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }

        private void TrySwitchToBigPicture()
        {
            IntPtr bigPictureWindowHandle = Win32.FindWindow(BigPictureWindowClassName, BigPictureWindowTitle);
            if (bigPictureWindowHandle == IntPtr.Zero) return;

            Win32.SwitchToThisWindow(bigPictureWindowHandle, true);

            Log("Forces Big Picture window focus");
        }

        private void StartProcessMonitor()
        {
            var query = new WqlEventQuery(
                "__InstanceCreationEvent", 
                new TimeSpan(0, 0, 1),
                "TargetInstance isa \"Win32_Process\""
                );

            _processMonitor = new ManagementEventWatcher { Query = query };
            _processMonitor.EventArrived += ProcessMonitorOnEventArrived;
            _processMonitor.Start();
        }

        private void ProcessMonitorOnEventArrived(object sender, EventArrivedEventArgs e)
        {
            var processManagementObject = (ManagementBaseObject)e.NewEvent.Properties["TargetInstance"].Value;

            int parentProcessId = (int)(uint)processManagementObject.Properties["ParentProcessId"].Value;
            int processId = int.Parse((string)processManagementObject.Properties["Handle"].Value);
            string processPath = (string)processManagementObject.Properties["ExecutablePath"].Value;
            
            Process process = null;
            try
            {
                process = Process.GetProcessById(processId);
            }
            catch
            {
                Log("Error getting process: " + processId);
            }

            // Woops, too late to get it.
            if (process == null) return;

            // Cheking if is Steam process started.
            if (!IsSteamProcessRunnig() && IsSteamProcess(process))
            {
                TryWatchSteamProcess();
                if (IsSteamProcessRunnig()) StartFocusWatcher();
                return;
            }

            // This is some other process not connected to Steam.
            if (!IsSteamChildProcessId(parentProcessId)) return;

            // Looks like a Steam child process, a game maybe.
            _steamChildProcesses[process.Id] = process;
            process.EnableRaisingEvents = true;
            process.Exited += ChildProcessOnExited;

            Log("Process started. Id: " + processId + ", parendt id: " + parentProcessId + ", path: " + processPath);
        }

        private void ChildProcessOnExited(object sender, EventArgs eventArgs)
        {
            var process = (Process)sender;
            Log("Child process exited: " + process.Id);

            Thread.Sleep(WatcherPollTime);

            Process item;
            _steamChildProcesses.TryRemove(process.Id, out item);

            Log("Total child processes: " + _steamChildProcesses.Count);
        }
    }
}
