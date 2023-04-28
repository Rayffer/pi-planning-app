﻿using System;
using System.Globalization;
using System.Windows.Data;

namespace PiPlanningApp.Converters
{
    internal class FloatDivideCeilingRoundingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int integerValue &&
                parameter is string stringValue &&
                int.TryParse(stringValue, out var numberToDivideBy))
            {
                return Math.Ceiling(integerValue / (float)numberToDivideBy);
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
