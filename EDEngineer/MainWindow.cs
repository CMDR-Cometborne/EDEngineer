﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using EDEngineer.Models;
using EDEngineer.Utils.System;
using EDEngineer.Utils.UI;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using DataGridCell = System.Windows.Controls.DataGridCell;

namespace EDEngineer
{
    public partial class MainWindow
    {
        private readonly MainWindowViewModel viewModel;

        public MainWindow()
        {
            var procName = Process.GetCurrentProcess().ProcessName;
            var processes = Process.GetProcessesByName(procName);

            if (processes.Length > 1)
            {
                System.Windows.Forms.MessageBox.Show($"EDEngineer already running, you can bring it up with your shortcut ({SettingsManager.Shortcut}).",
                    "Oops", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Application.Current.Shutdown();
                return;
            }

            SettingsManager.Init();

            InitializeComponent();
            viewModel = new MainWindowViewModel();
            DataContext = viewModel;

            var blueprintsView = new CollectionViewSource {Source = viewModel.Blueprints}.View;
            viewModel.Filters.Monitor(blueprintsView, viewModel.State.Cargo.Select(c => c.Value));
            Blueprints.ItemsSource = blueprintsView;

            Commodities.ItemsSource = viewModel.FilterView(Kind.Commodity, new CollectionViewSource { Source = viewModel.State.Cargo }.View);
            Materials.ItemsSource = viewModel.FilterView(Kind.Material, new CollectionViewSource { Source = viewModel.State.Cargo }.View);
            Data.ItemsSource = viewModel.FilterView(Kind.Data, new CollectionViewSource { Source = viewModel.State.Cargo }.View);
        }

        private void MainWindowLoaded(object sender, RoutedEventArgs args)
        {
            icon = TrayIconManager.Init((o, e) => ShowWindow(), (o, e) => Close(), ConfigureShortcut);

            var shortcut = SettingsManager.Shortcut;

            HotkeyManager.RegisterHotKey(this, (Keys)new KeysConverter().ConvertFromString(shortcut));
        }

        private void ConfigureShortcut(object sender, EventArgs e)
        {
            string shortcut;
            ignoreShortcut = true;
            if (Math.Abs(Opacity) > 0.99)
            {
                HideWindow();
            }

            HotkeyManager.UnregisterHotKey(this);
            if (ShortcutPrompt.ShowDialog(SettingsManager.Shortcut, out shortcut))
            {
                SettingsManager.Shortcut = shortcut;
                HotkeyManager.UnregisterHotKey(this);
                HotkeyManager.RegisterHotKey(this, (Keys) new KeysConverter().ConvertFromString(shortcut));
            }
            else
            {
                HotkeyManager.RegisterHotKey(this, (Keys)new KeysConverter().ConvertFromString(SettingsManager.Shortcut));
            }

            ignoreShortcut = false;
        }

        private void CheckAllButtonClicked(object sender, RoutedEventArgs e)
        {
            viewModel.Filters.ChangeAllFilters(true);
        }

        private void UncheckAllButtonClicked(object sender, RoutedEventArgs e)
        {
            viewModel.Filters.ChangeAllFilters(false);
        }

        private readonly Regex forbiddenCharacters = new Regex("[^0-9.-]+");
        private void EntryCountTextBoxOnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            int newCount;
            if (forbiddenCharacters.IsMatch(e.Text))
            {
                e.Handled = true;
            }

        }

        private BindingBase binding;
        private void EntryCountTextBox_OnGotFocus(object sender, RoutedEventArgs e)
        {
            var box = (System.Windows.Controls.TextBox)sender;
            binding = box.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty).ParentBindingBase;
            BindingOperations.ClearBinding(box, System.Windows.Controls.TextBox.TextProperty);
        }

        private void EntryCountTextBox_OnLostFocus(object sender, RoutedEventArgs e)
        {
            var box = (System.Windows.Controls.TextBox)sender;

            int newCount;
            if (int.TryParse(box.Text, out newCount))
            {
                var entry = (Entry)box.Tag;
                viewModel.UserChange(entry, newCount - entry.Count);
            }

            BindingOperations.SetBinding(box, System.Windows.Controls.TextBox.TextProperty, binding);
        }

        private void IncrementButtonClicked(object sender, RoutedEventArgs e)
        {
            var entry = (Entry) ((Button) sender).Tag;
            viewModel.UserChange(entry, 1);
        }

        private void DecrementButtonClicked(object sender, RoutedEventArgs e)
        {
            var entry = (Entry) ((Button) sender).Tag;
            viewModel.UserChange(entry, -1);
        }

        private void ChangeFolderButtonClicked(object sender, RoutedEventArgs e)
        {
            viewModel.LoadState(true);
        }

        private void PreviewMouseLeftButtonDownHandler(object sender, MouseButtonEventArgs e)
        {
            var cell = (DataGridCell) sender;
            if (cell.Column.Header == null)
            {
                var toggleButton = FindVisualParent<ToggleButton>(Mouse.DirectlyOver as DependencyObject);
                if (toggleButton?.IsChecked != null)
                {
                    toggleButton.IsChecked = !toggleButton.IsChecked.Value;
                }
                e.Handled = true;
            }
            else if (!cell.IsEditing)
            {
                var row = FindVisualParent<DataGridRow>(cell);
                if (row != null)
                {
                    row.IsSelected = !row.IsSelected;
                    e.Handled = true;
                }
            }
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (true)
            {
                var parentObject = VisualTreeHelper.GetParent(child);

                if (parentObject == null)
                {
                    return null;
                }

                var parent = parentObject as T;
                if (parent != null)
                {
                    return parent;
                }

                child = parentObject;
            }
        }

        private void BlueprintsDataGridLoaded(object sender, RoutedEventArgs e)
        {
            var newStyle = new Style
            {
                BasedOn = Blueprints.CellStyle,
                TargetType = typeof (DataGridCell)
            };

            newStyle.Setters.Add(new EventSetter(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(PreviewMouseLeftButtonDownHandler)));
            Blueprints.CellStyle = newStyle;
        }

        bool transitionning = false;
        private void HideWindow()
        {
            if (!transitionning)
            {
                transitionning = true;
                var sb = (Storyboard)FindResource("WindowDeactivated");
                Storyboard.SetTarget(sb, this);
                sb.Begin();
            }
        }

        private void ShowWindow()
        {
            if (!transitionning)
            {
                Show();
                Focus();
                transitionning = true;
                var sb = (Storyboard) FindResource("WindowActivated");
                Storyboard.SetTarget(sb, this);
                sb.Begin();
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            handle = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            handle?.AddHook(WndProc);
        }
        
        private HwndSource handle;
        private IDisposable icon;
        private bool ignoreShortcut = false;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == HotkeyManager.WM_HOTKEY && !ignoreShortcut)
            {
                if (Math.Abs(Opacity) < 0.01)
                {
                    ShowWindow();
                }
                else
                {
                    HideWindow();
                }
            }

            return IntPtr.Zero;
        }

        protected override void OnClosed(EventArgs e)
        {
            if (handle != null)
            {
                HotkeyManager.UnregisterHotKey(this);
                handle.RemoveHook(WndProc);
                icon.Dispose();
            }
        }

        private void WindowActivatedCompleted(object sender, EventArgs e)
        {
            transitionning = false;
        }

        private void WindowDeactivatedCompleted(object sender, EventArgs e)
        {
            Hide();
            transitionning = false;
        }
    }
}