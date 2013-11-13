/*
 * This file is part of the Face Fusion project. 
 *
 * Copyright (c) 2013 Joshua Blake
 *
 * This code is licensed to you under the terms of the MIT license.
 * See https://facefusion.codeplex.com/license for a copy of the license.
 */

using System.Windows;
using FaceFusion.ViewModels;

namespace FaceFusion
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MainViewModel _mainViewModel;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            this.InitializeComponent();

            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
            Application.Current.Exit += new ExitEventHandler(Current_Exit);
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _mainViewModel = new MainViewModel();
            this.DataContext = _mainViewModel;
        }

        void Current_Exit(object sender, ExitEventArgs e)
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.Dispose();
            }
        }
    }
}
