using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DocDbQueryExecuter
{
    /// <summary>
    /// Interaction logic for MainPage.xaml
    /// </summary>
    public partial class MainPage : Window
    {
        //private const string DocDbEndPoint = "https://localhost:8081";
        //private const string DocDbKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        DocDbBaseClient dbBase;

        /// <summary>
        /// The MainPageS
        /// </summary>
        public MainPage()
        {
            //dbBase = new DocDbBaseClient(DocDbEndPoint, DocDbKey);
            InitializeComponent();
            GetAppLevelAccounts();
        }

        private void GetAppLevelAccounts()
        {
            FillTree(AccountsList());
        }

        private Dictionary<string, string> AccountsList()
        {
            Dictionary<string, string> cosmosItems = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default["AccountList"]?.ToString()))
            {
                cosmosItems = JsonConvert.DeserializeObject<Dictionary<string, string>>(Properties.Settings.Default["AccountList"]?.ToString());
            }

            return cosmosItems;
        }

        private Dictionary<string, string> RemoveAccount(string accountName)
        {
            // Get Values from Settings
            Dictionary<string, string> cosmosItems = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default["AccountList"]?.ToString()))
            {
                cosmosItems = JsonConvert.DeserializeObject<Dictionary<string, string>>(Properties.Settings.Default["AccountList"]?.ToString());
            }

            // If Account Items are more than one remove selected item
            if (cosmosItems.Count > 0 && !string.IsNullOrWhiteSpace(accountName))
            {
                cosmosItems.Remove(accountName);
            }

            // Update remaining items in Settings
            Properties.Settings.Default["AccountList"] = JsonConvert.SerializeObject(cosmosItems);
            Properties.Settings.Default.Save();

            return cosmosItems;
        }

        /// <summary>
        /// The tree_SelectedItemChanged
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs e</param>
        private void tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            MenuItem docItem = tree.SelectedItem as MenuItem;
            if (docItem != null && dbBase != null)
            {
                string documentItem = docItem.Title;
                var strValue = dbBase.GetItemJson(docItem).Result;
                BindTextBox(strValue);
            }
        }

        /// <summary>
        /// The BindTextBox
        /// </summary>
        /// <param name="jsonString">The jsonString</param>
        private void BindTextBox(string jsonString)
        {
            Dispatcher.Invoke(() =>
            {
                documentViewer.Document.Blocks.Clear();
                if (!string.IsNullOrWhiteSpace(jsonString))
                {
                    var result = jsonString?.ToString().Replace("\n", "");
                    documentViewer.AppendText(result);
                }
            });
        }

        /// <summary>
        /// The FillTree
        /// </summary>
        private void FillTree(Dictionary<string, string> addedAccounts)
        {
            Dispatcher.Invoke(() =>
            {
                tree.Items.Clear();
                if (addedAccounts.Count > 0)
                {
                    foreach (var account in addedAccounts)
                    {
                        dbBase = new DocDbBaseClient(account.Key, account.Value);

                        var result = dbBase.GetDBList().Result;
                        if (result != null)
                        {
                            tree.Items.Add(result);
                        }
                    }

                    ContextMenu contextMenu = new ContextMenu();

                    // Refresh Menu item
                    System.Windows.Controls.MenuItem rootRefresh = new System.Windows.Controls.MenuItem();
                    rootRefresh.Header = "Refresh";
                    rootRefresh.Click += RootRefresh_Click;
                    contextMenu.Items.Add(rootRefresh);

                    // Remove Menu item
                    System.Windows.Controls.MenuItem rootRemove = new System.Windows.Controls.MenuItem();
                    rootRemove.Header = "Remove";
                    rootRemove.Click += RootRemove_Click;
                    //rootRemove.MouseUp += RootRemove_Click;
                    contextMenu.Items.Add(rootRemove);

                    tree.ContextMenu = contextMenu;
                }
            });
        }

        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            TreeViewItem treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);

            if (treeViewItem != null)
            {
                treeViewItem.Focus();
                e.Handled = true;
            }
        }

        static TreeViewItem VisualUpwardSearch(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem))
                source = VisualTreeHelper.GetParent(source);

            return source as TreeViewItem;
        }

        /// <summary>
        /// The RootRefresh_Click
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs e</param>
        private void RootRefresh_Click(object sender, RoutedEventArgs e)
        {
            GetAppLevelAccounts();
        }

        /// <summary>
        /// The RootRemove_Click
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The eventArgs</param>
        //private void RootRemove_Click(object sender, MouseEventArgs e)
        private void RootRemove_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = (MenuItem)tree.SelectedItem;
            var accountsList = RemoveAccount(selectedItem.Title);
            FillTree(accountsList);
        }

        /// <summary>
        /// The btnExecute_Click
        /// </summary>
        /// <param name="sender">The sender</param>
        /// <param name="e">The EventArgs e</param>
        private async void btnExecute_Click(object sender, RoutedEventArgs e)
        {
            var selectedTab = tabQueryControl.SelectedItem as TabItem;
            var selectedDoc = tree?.SelectedItem as MenuItem;

            if (selectedDoc != null
                && !string.IsNullOrWhiteSpace(selectedDoc.DbName)
                && !string.IsNullOrWhiteSpace(selectedDoc.CollectioName)
                && (!string.IsNullOrWhiteSpace(txtQueryBox.Text)
                || !string.IsNullOrWhiteSpace(txtUpdateQueryBox.Text)
                || !string.IsNullOrWhiteSpace(txtDeleteQueryBox.Text)))
            {
                string strValue = string.Empty;
                try
                {
                    await ExecuteQueryTask(selectedTab.Header.ToString(), selectedDoc).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error : { ex.Message}");
                }
            }
            else
            {
                MessageBox.Show(string.IsNullOrWhiteSpace(selectedDoc.DbName) || string.IsNullOrWhiteSpace(selectedDoc.CollectioName) ? "Please select collection." : "Enter valid query");
            }
        }

        /// <summary>
        /// The ExecuteQueryTask
        /// </summary>
        /// <param name="tabName">The tabName</param>
        /// <param name="menuItem">The menuItem</param>
        /// <returns></returns>
        private async Task ExecuteQueryTask(string tabName, MenuItem menuItem)
        {
            string strValue = string.Empty;
            switch (tabName.ToUpper())
            {
                case "SEARCH":
                    strValue = dbBase.GetDocumentInfo(menuItem, txtQueryBox.Text);
                    break;
                case "UPDATE":
                    strValue = await dbBase.UpdateDocuments(menuItem, txtUpdateQueryBox.Text).ConfigureAwait(false);
                    break;
                case "DELETE":
                    await dbBase.DeleteItems(menuItem, txtDeleteQueryBox.Text).ConfigureAwait(false);
                    GetAppLevelAccounts();
                    strValue = "Records deleted.";
                    break;
                default:
                    break;
            }

            BindTextBox(strValue);
        }

        /// <summary>
        /// The AddItem_Click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddItem_Click(object sender, RoutedEventArgs e)
        {
            ConnectionDialog addDialog = new ConnectionDialog();
            addDialog.Closed += AddDialog_Closed;
            addDialog.Show();
        }

        private void AddDialog_Closed(object sender, EventArgs e)
        {
            GetAppLevelAccounts();
        }
    }
}
