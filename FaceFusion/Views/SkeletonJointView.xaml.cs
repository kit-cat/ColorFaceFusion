/*
 * This file is part of the Face Fusion project. 
 *
 * Copyright (c) 2013 Joshua Blake
 *
 * This code is licensed to you under the terms of the MIT license.
 * See https://facefusion.codeplex.com/license for a copy of the license.
 */

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FaceFusion.ViewModels;

namespace FaceFusion.Views
{
    /// <summary>
    /// Interaction logic for SkeletonJointView.xaml
    /// </summary>
    public partial class SkeletonJointView : UserControl
    {
        public SkeletonJointViewModel ViewModel
        {
            get
            {
                return this.DataContext as SkeletonJointViewModel;
            }
        }

        public SkeletonJointView()
        {
            InitializeComponent();

            if (ViewModel != null)
            {
                ViewModel.FrameUpdated += FrameUpdated;
            }

            this.DataContextChanged += new DependencyPropertyChangedEventHandler(VM_DataContextChanged);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (ViewModel == null)
                return;
    
            var pen = new Pen(Brushes.Blue, 2.0);

            foreach (var joint in ViewModel.Joints)
            {
                drawingContext.DrawEllipse(null, pen, new Point(joint.X, joint.Y), 4, 4);
            }
        }

        void FrameUpdated(object sender, EventArgs e)
        {
            this.InvalidateVisual();
        }

        void VM_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var oldVM = e.OldValue as SkeletonJointViewModel;
            if (oldVM != null)
            {
                oldVM.FrameUpdated -= FrameUpdated;
            }

            var newVM = e.NewValue as SkeletonJointViewModel;
            if (newVM != null)
            {
                newVM.FrameUpdated += FrameUpdated;
            }
        }
        
    }
}
