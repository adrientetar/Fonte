﻿/**
 * Copyright 2018, Adrien Tétar. All Rights Reserved.
 */

namespace Fonte.App.Commands
{
    using System;
    using System.Windows.Input;
    using Windows.UI.Xaml;

    public class ReverseAllPathsCommand : ICommand
    {
#pragma warning disable CS0067
        public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            var layer = (Data.Layer)parameter;

            foreach (var path in layer.Paths)
            {
                path.Reverse();
            }
            ((App)Application.Current).InvalidateData();
        }
    }
}
