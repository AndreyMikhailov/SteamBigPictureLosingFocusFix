using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace SteamBigPictureLosingFocusFix
{
    public partial class MainWindow : Window
    {
        private const string ProgramName = "Steam Big Picture Losing Focus Fix";
        private readonly RegistryKey _autorunRegistryKey;
        private readonly string _currentExecutionPath = Assembly.GetEntryAssembly().Location;

        public MainWindow()
        {
            InitializeComponent();

            _autorunRegistryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

            if (_autorunRegistryKey == null)
            {
                MessageBox.Show("Failed accessing registry");
                Application.Current.Shutdown();
                return;
            }

            object keyValue = _autorunRegistryKey.GetValue(ProgramName);

            if (keyValue == null)
            {
                _autorunRegistryKey.SetValue(ProgramName, _currentExecutionPath);
                AutostartMenuItem.IsChecked = true;
                return;
            }

            var keyValueStr = keyValue as string;
            if (keyValueStr == null) return;

            if (keyValueStr == "")
            {
                AutostartMenuItem.IsChecked = false;
                return;
            }

            if (keyValueStr != _currentExecutionPath)
                _autorunRegistryKey.SetValue(ProgramName, _currentExecutionPath);

            AutostartMenuItem.IsChecked = true;
        }

        private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void AutostartMenuItem_OnChecked(object sender, RoutedEventArgs e)
        {
            _autorunRegistryKey.SetValue(ProgramName, _currentExecutionPath);
        }

        private void AutostartMenuItem_OnUnchecked(object sender, RoutedEventArgs e)
        {
            _autorunRegistryKey.SetValue(ProgramName, "");
        }
    }
}
