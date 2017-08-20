﻿using System;
using Windows.UI.Xaml.Data;

namespace Dopamine.UWP.Presentation.Converters
{
    public class StringToUpperConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var str = value?.ToString();

            return !string.IsNullOrEmpty(str) ? str.ToUpper() : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
