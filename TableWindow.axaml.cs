using Avalonia.Controls;
using System.Collections.Generic;
using Лаб3ПА.Models;

namespace Лаб3ПА
{
    public partial class TableWindow : Window
    {
        public TableWindow()
        {
            InitializeComponent();
        }

        // Конструктор, який приймає список результатів
        public TableWindow(List<SearchStat> stats) : this()
        {
            var grid = this.FindControl<DataGrid>("StatsGrid");
            if (grid != null)
            {
                grid.ItemsSource = stats;
            }
        }
    }
}