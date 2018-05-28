using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DocDbQueryExecuter
{
    /// <summary>
    /// Interaction logic for ConnectionDialog.xaml
    /// </summary>
    public partial class ConnectionDialog : Window
    {
        public ConnectionDialog()
        {
            InitializeComponent();
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, string> cosmosItems = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default["AccountList"]?.ToString()))
            {
                cosmosItems = JsonConvert.DeserializeObject<Dictionary<string, string>>(Properties.Settings.Default["AccountList"]?.ToString());
            }

            if (!cosmosItems.ContainsKey(txtUrl.Text))
            {
                cosmosItems.Add(txtUrl.Text, txtSecret.Text);
                Properties.Settings.Default["AccountList"] =  JsonConvert.SerializeObject(cosmosItems);
                Properties.Settings.Default.Save();
            }
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
