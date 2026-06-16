using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using AnnoDesigner.ViewModels;

namespace AnnoDesigner
{
    /// <summary>
    /// Interaction logic for ImportSavegameWindow.xaml. Picks which imported island to load.
    /// </summary>
    public partial class ImportSavegameWindow : Window
    {
        public ImportSavegameWindow()
        {
            InitializeComponent();
        }

        public ImportSavegameWindow(MainViewModel context, IEnumerable<string> choices) : this()
        {
            DataContext = context;

            foreach (var choice in choices)
            {
                islandList.Items.Add(choice);
            }

            if (islandList.Items.Count > 0)
            {
                islandList.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Index of the selected island, or -1 if cancelled / nothing selected.
        /// </summary>
        public int SelectedIndex => islandList.SelectedIndex;

        /// <summary>
        /// Shows the picker and returns the chosen island index, or -1 if cancelled.
        /// </summary>
        public static int Prompt(MainViewModel context, IEnumerable<string> choices)
        {
            var window = new ImportSavegameWindow(context, choices);
            window.ShowDialog();

            return window.DialogResult == true ? window.SelectedIndex : -1;
        }

        private void IslandList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (islandList.SelectedIndex >= 0)
            {
                DialogResult = true;
                Close();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (islandList.SelectedIndex < 0)
            {
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
