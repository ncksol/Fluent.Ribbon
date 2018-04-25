﻿namespace FluentTest
{
    using System;
    using System.Linq;
    using System.Windows;
    using Fluent;
    using MahApps.Metro.Controls;

    /// <summary>
    /// Interaction logic for MahMetroWindow.xaml
    /// </summary>
    [CLSCompliant(false)] // Because MetroWindow is not CLSCompliant
    public partial class MahMetroWindow : IRibbonWindow
    {
        public MahMetroWindow()
        {
            this.InitializeComponent();

            this.Loaded += this.MahMetroWindow_Loaded;
        }

        private void MahMetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.TitleBar = this.FindChild<RibbonTitleBar>("RibbonTitleBar");
            this.TitleBar.InvalidateArrange();
            this.TitleBar.UpdateLayout();

            this.SyncThemeManagers();

            ThemeManager.IsThemeChanged += (o, args) => this.SyncThemeManagers();
        }

        private void SyncThemeManagers()
        {
            // Sync Fluent and MahApps ThemeManager
            var fluentAppStyle = ThemeManager.DetectAppStyle();
            var appTheme = MahApps.Metro.ThemeManager.AppThemes.First(x => x.Name == fluentAppStyle.Item1.Name);
            var accent = MahApps.Metro.ThemeManager.Accents.First(x => x.Name == fluentAppStyle.Item2.Name);
            MahApps.Metro.ThemeManager.ChangeAppStyle(this, accent, appTheme);
        }

        #region TitelBar

        /// <summary>
        /// Gets ribbon titlebar
        /// </summary>
        public RibbonTitleBar TitleBar
        {
            get { return (RibbonTitleBar)this.GetValue(TitleBarProperty); }
            private set { this.SetValue(TitleBarPropertyKey, value); }
        }

        // ReSharper disable once InconsistentNaming
        private static readonly DependencyPropertyKey TitleBarPropertyKey = DependencyProperty.RegisterReadOnly(nameof(TitleBar), typeof(RibbonTitleBar), typeof(MahMetroWindow), new PropertyMetadata());

        /// <summary>
        /// <see cref="DependencyProperty"/> for <see cref="TitleBar"/>.
        /// </summary>
        public static readonly DependencyProperty TitleBarProperty = TitleBarPropertyKey.DependencyProperty;

        #endregion
    }
}