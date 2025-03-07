﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using GetText;

using Orts.Common;

namespace Orts.Menu
{
    internal class ComboBoxItem<T>
    {
        public T Key { get; }
        public string Value { get; }

        public ComboBoxItem(T key, string value)
        {
            Key = key;
            Value = value;
        }

        public static void SetDataSourceMembers(ComboBox comboBox)
        {
            comboBox.DisplayMember = nameof(ComboBoxItem<int>.Value);
            comboBox.ValueMember = nameof(ComboBoxItem<int>.Key);
        }

        internal ComboBoxItem() { }
    }

    public static class ComboBoxExtension
    {
        /// <summary>
        /// Populates combobox items from an enum.
        /// Display members are the enum value description attributes
        /// Value Members are the enum value
        /// </summary>
        public static void DataSourceFromList<T>(this ComboBox comboBox, IEnumerable<T> source, Func<T, string> lookup)
        {
            if (comboBox == null)
                throw new ArgumentNullException(nameof(comboBox));

            comboBox.DataSource = FromList(source, lookup);
            ComboBoxItem<T>.SetDataSourceMembers(comboBox);
        }

        /// <summary>
        /// Populates combobox items from an enum.
        /// Display members are the enum value description attributes
        /// Value Members are the enum value
        /// </summary>
        public static void DataSourceFromEnum<T>(this ComboBox comboBox, ICatalog catalog) where T : Enum
        {
            if (comboBox == null)
                throw new ArgumentNullException(nameof(comboBox));

            comboBox.DataSource = FromEnum<T>(catalog);
            ComboBoxItem<T>.SetDataSourceMembers(comboBox);
        }

        /// <summary>
        /// Populates combobox items from an enum.
        /// Display members are the enum value description attributes
        /// Value Members are the int values of the enums
        /// </summary>
        public static void DataSourceFromEnumIndex<T>(this ComboBox comboBox, ICatalog catalog) where T : Enum
        {
            if (comboBox == null)
                throw new ArgumentNullException(nameof(comboBox));

            comboBox.DataSource = FromEnumValue<T>(catalog);
            ComboBoxItem<T>.SetDataSourceMembers(comboBox);

        }

        private static IList<ComboBoxItem<T>> FromEnum<T>(ICatalog catalog) where T : Enum
        {
            string context = EnumExtension.EnumDescription<T>();
            return (from data in EnumExtension.GetValues<T>()
                    select new ComboBoxItem<T>(data, 
                    string.IsNullOrEmpty(context) ? catalog.GetString(data.GetDescription()) : catalog.GetParticularString(context, data.GetDescription()))).ToList();
        }

        private static IList<ComboBoxItem<int>> FromEnumValue<E>(ICatalog catalog) where E : Enum
        {
            string context = EnumExtension.EnumDescription<E>();
            return (from data in EnumExtension.GetValues<E>()
                    select new ComboBoxItem<int>(
                        Convert.ToInt32(data, System.Globalization.CultureInfo.InvariantCulture),
                        string.IsNullOrEmpty(context) ? catalog.GetString(data.GetDescription()) : catalog.GetParticularString(context, data.GetDescription()))).ToList();
        }

        /// <summary>
        /// Returns a new IList<ComboBoxItem<T>> created from source enum.
        /// Keys and values are mapped from enum values, typically keys are enum values or enum value names
        /// </summary>
        private static IList<ComboBoxItem<T>> FromEnumCustomLookup<E, T>(Func<E, T> keyLookup, Func<E, string> valueLookup) where E : Enum
        {
            return (from data in EnumExtension.GetValues<E>()
                    select new ComboBoxItem<T>(keyLookup(data), valueLookup(data))).ToList();
        }

        /// <summary>
        /// Returns a new IList<ComboBoxItem<T>> created from source list.
        /// Keys are mapped from list items, display values are mapped through lookup function
        /// </summary>
        private static IList<ComboBoxItem<T>> FromList<T>(IEnumerable<T> source, Func<T, string> lookup)
        {
            try
            {
                return (from item in source
                        select new ComboBoxItem<T>(item, lookup(item))).ToList();
            }
            catch (ArgumentException)
            {
                return new List<ComboBoxItem<T>>();
            }
        }

    }
}
