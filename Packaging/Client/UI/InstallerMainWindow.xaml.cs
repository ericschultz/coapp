﻿//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Packaging.Client.UI {
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Data;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Animation;

    /// <summary>
    ///   Interaction logic for InstallerMainWindow.xaml
    /// </summary>
    public partial class InstallerMainWindow : Window {
        private bool _actionTaken;
        public Installer Installer;

        public InstallerMainWindow(Installer installer) {
            Opacity = 0;
            Installer = installer;
            InitializeComponent();

            OrganizationName.SetBinding(TextBlock.TextProperty, new Binding("Organization") {Source = Installer});
            ProductName.SetBinding(TextBlock.TextProperty, new Binding("Product") {Source = Installer});

            // package icon disabled until after RC
            // PackageIcon.SetBinding(Image.SourceProperty, new Binding("PackageIcon") { Source = Installer });

            DescriptionText.SetBinding(TextBlock.TextProperty, new Binding("Description") {Source = Installer});
            WhichVersionToInstall.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("InstallChoices") {Source = Installer});
            WhichVersionToInstall.DisplayMemberPath = "Value";
            WhichVersionToInstall.SelectedValuePath = "Key";
            WhichVersionToInstall.SetBinding(Selector.SelectedValueProperty, new Binding("Choice") {Source = Installer});

            WhichVersionToInstall.SelectedIndex = 0;

            ProductVersion.SetBinding(TextBlock.TextProperty, new Binding("ProductVersion") {Source = Installer});
            InstallButton.SetBinding(IsEnabledProperty, new Binding("ReadyToInstall") {Source = Installer});
            InstallButton.SetBinding(ToolTipProperty, new Binding("InstallButtonText") {Source = Installer});
            InstallText.SetBinding(TextBlock.TextProperty, new Binding("InstallButtonText") {Source = Installer});
            RemoveButton.SetBinding(VisibilityProperty, new Binding("RemoveButtonVisibility") {Source = Installer});
            RemoveAdvanced.SetBinding(VisibilityProperty, new Binding("RemoveButtonVisibility") {Source = Installer});
            InstallationProgress.SetBinding(RangeBase.ValueProperty, new Binding("Progress") {Source = Installer});
            CancelButton.SetBinding(VisibilityProperty, new Binding("CancelButtonVisibility") {Source = Installer});

            RemoveContextMenu.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("RemoveChoices") {Source = Installer});

            try {
                VisibilityAnimation.SetAnimationType(RemoveButton, VisibilityAnimation.AnimationType.Fade);
                VisibilityAnimation.SetAnimationType(InstallButton, VisibilityAnimation.AnimationType.Fade);
                VisibilityAnimation.SetAnimationType(InstallationProgress, VisibilityAnimation.AnimationType.Fade);
                VisibilityAnimation.SetAnimationType(WhichVersionToInstall, VisibilityAnimation.AnimationType.Fade);
                VisibilityAnimation.SetAnimationType(CancelButton, VisibilityAnimation.AnimationType.Fade);
            } catch {
            }

            Loaded += (src, evnt) => {
                Topmost = false;
                if (Opacity <= 0 && Installer.HasPackage) {
                    Installer.Ping = true;
                    ShowInTaskbar = true;
                    ((Storyboard)FindResource("showWindow")).Begin();
                    if (WhichVersionToInstall.SelectedIndex == -1) {
                        WhichVersionToInstall.SelectedIndex = 0;
                    }
                }
            };

            Installer.Ready += (src, evnt) => Invoke(() => {
                if (!(Opacity > 0)) {
                    Installer.Ping = true;
                    ShowInTaskbar = true;
                    ((Storyboard)FindResource("showWindow")).Begin();
                    if (WhichVersionToInstall.SelectedIndex == -1) {
                        WhichVersionToInstall.SelectedIndex = 0;
                    }
                }
            });

            Installer.Finished += (src, evnt) => Invoke(() => {
                ((Storyboard)FindResource("hideWindow")).Completed += (ss, ee) => Invoke(Close);
                ((Storyboard)FindResource("hideWindow")).Begin();
            });
        }

        internal void FixFont() {
            if (DescriptionText.ActualHeight > 150 && DescriptionText.FontSize > 9) {
                DescriptionText.FontSize -= .5;
                Task.Factory.StartNew(() => {
                    Thread.Sleep(20);
                    Dispatcher.BeginInvoke((Action)(FixFont));
                });
            }
        }

        internal void OnLoaded(object sender, RoutedEventArgs e) {
            FixFont();
        }

        private void MenuItemClick(object sender, RoutedEventArgs e) {
            var v = e.OriginalSource as MenuItem;
            if (v != null) {
                var commandAction = v.CommandParameter as Action;
                if (commandAction != null) {
                    InstallationProgress.Foreground = new SolidColorBrush(Colors.Red);
                    TakeAction();
                    commandAction();
                }
            }
        }

        private void HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            DragMove();
        }

        private void CloseBtnClick(object sender, RoutedEventArgs e) {
            Installer.CancelRequested = true;
        }

        private void ShowRemoveMenu(object sender, RoutedEventArgs e) {
            RemoveContextMenu.PlacementTarget = RemoveAdvanced;
            RemoveContextMenu.Placement = PlacementMode.Custom;

            RemoveContextMenu.CustomPopupPlacementCallback = (size, targetSize, offset) => new[] {new CustomPopupPlacement(new Point(RemoveAdvanced.Width, (RemoveAdvanced.Height - size.Height) - 10), PopupPrimaryAxis.None)};
            RemoveContextMenu.IsOpen = !RemoveContextMenu.IsOpen;
        }

        private void InstallButtonClick(object sender, RoutedEventArgs e) {
            if (!_actionTaken) {
                TakeAction();
                Installer.Install();
            }
        }

        protected void Invoke(Action action) {
            Dispatcher.Invoke(action);
        }

        private void TakeAction() {
            _actionTaken = true;

            InstallationProgress.Visibility = Visibility.Visible;
            WhichVersionToInstall.Visibility = Visibility.Hidden;
            InstallButton.Visibility = Visibility.Hidden;
            RemoveButton.Visibility = Visibility.Hidden;
            RemoveAdvanced.Visibility = Visibility.Hidden;

            ((Storyboard)FindResource("slideTrans")).Begin();
        }

        private void RemoveButtonClick(object sender, RoutedEventArgs e) {
            if (!_actionTaken) {
                TakeAction();
                InstallationProgress.Foreground = new SolidColorBrush(Colors.Red);
                Installer.RemoveAll();
            }
        }

        private void DescriptionText_SourceUpdated(object sender, DataTransferEventArgs e) {
            if (DescriptionText.ActualHeight > 150) {
                DescriptionText.FontSize = 12;
            }
        }
    }
}