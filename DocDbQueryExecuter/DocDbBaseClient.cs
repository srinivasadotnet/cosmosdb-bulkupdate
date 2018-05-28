using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DocDbQueryExecuter
{
    /// <summary>
    /// The DocDbBaseClient
    /// </summary>
    public class DocDbBaseClient
    {
        // The Client
        DocumentClient client;

        string endpoint = string.Empty;
        string secret = string.Empty;

        /// <summary>
        /// The DocDbBaseClient
        /// </summary>
        /// <param name="endpoint">The endpoint</param>
        /// <param name="key">The key</param>
        public DocDbBaseClient(string endpoint, string key)
        {
            client = new DocumentClient(new Uri(endpoint), key,
                        new ConnectionPolicy
                        {
                            ConnectionMode = ConnectionMode.Direct,
                            ConnectionProtocol = Protocol.Https,
                            EnableEndpointDiscovery = true
                        });

            this.endpoint = endpoint;
            this.secret = key;
        }

        /// <summary>
        /// The GetDBList
        /// </summary>
        /// <returns></returns>
        public async Task<MenuItem> GetDBList()
        {
            MenuItem rootItem = new MenuItem { Title = this.endpoint, Level = 0, Secret = this.secret };
            var dbList = await client.ReadDatabaseFeedAsync().ConfigureAwait(false);
            foreach (var dbItem in dbList)
            {
                var dbModel = new MenuItem
                {
                    Title = dbItem.Id,
                    Level = 1
                };

                var dbCollectionList = await client.ReadDocumentCollectionFeedAsync(UriFactory.CreateDatabaseUri(dbItem.Id)).ConfigureAwait(false);// (dbItem.SelfLink).ConfigureAwait(false);
                                                                                                                                                   //List<MenuItem> dbCollection = new List<MenuItem>();

                foreach (var collectionItem in dbCollectionList)
                {
                    var collectionItemModel = new MenuItem { Title = collectionItem.Id, Level = 2, DbName = dbItem.Id, CollectioName = collectionItem.Id };
                    var docCollectionList = await client.ReadDocumentFeedAsync(UriFactory.CreateDocumentCollectionUri(dbItem.Id, collectionItem.Id)).ConfigureAwait(false);// (dbItem.SelfLink).ConfigureAwait(false);
                    foreach (Document docItem in docCollectionList)
                    {
                        collectionItemModel.Items.Add(new MenuItem
                        {
                            Title = docItem.Id,
                            DocSelfLink = docItem.SelfLink,
                            CollectionSelfLink = collectionItem.SelfLink,
                            Level = 3,
                            DbName = dbItem.Id,
                            CollectioName = collectionItem.Id
                        });
                    }
                    dbModel.Items.Add(collectionItemModel);
                }
                rootItem.Items.Add(dbModel);
            }

            return rootItem;
        }

        /// <summary>
        /// The UpdateDocuments
        /// </summary>
        /// <param name="menuItem">The menuItem</param>
        /// <param name="query">The query</param>
        /// <returns></returns>
        public async Task<string> UpdateDocuments(MenuItem menuItem, string query)
        {
            string result = string.Empty;
            if (string.IsNullOrWhiteSpace(query) == false)
            {
                var queryItems = Regex.Split(query, "WHERE", RegexOptions.IgnoreCase);
                string selectQuery = "SELECT * FROM c";
                if (queryItems.Count() == 2)
                {
                    selectQuery = $"{selectQuery} WHERE {queryItems.LastOrDefault()}";
                }

                var updateFields = queryItems.FirstOrDefault().Split(',');

                List<string> docItems = new List<string>();
                foreach (var documentItem in client.CreateDocumentQuery<Document>(menuItem.CollectionSelfLink, selectQuery, new FeedOptions { EnableCrossPartitionQuery = true }).ToList())
                {
                    foreach (var field in updateFields)
                    {
                        var fieldnValue = field.Split('=');
                        if (fieldnValue.Any())
                        {
                            var jsonNodeList = fieldnValue.FirstOrDefault().Replace("c.", "").Split('.').Select(n => n.Trim());
                            dynamic metadataInfo = documentItem.GetPropertyValue<JObject>(jsonNodeList.FirstOrDefault());

                            JToken token = metadataInfo.SelectToken(string.Join(".", jsonNodeList.Skip(1).ToArray()));
                            var fieldValue = fieldnValue.LastOrDefault().Replace("\\", "").Replace("\"", "").Trim();
                            token.Replace(fieldValue);

                            documentItem.SetPropertyValue(jsonNodeList.FirstOrDefault(), metadataInfo);
                        }
                    }

                    await client.ReplaceDocumentAsync(documentItem.SelfLink, documentItem).ConfigureAwait(false);
                    docItems.Add(JObject.Parse(documentItem?.ToString()).ToString());
                }
                result = docItems.Count > 0 ? $"[{string.Join(",", docItems)}]" : string.Empty;
            }
            return result;
        }

        /// <summary>
        /// The GetItemJson
        /// </summary>
        /// <param name="menuItem">The menuItem</param>
        /// <returns></returns>
        public async Task<string> GetItemJson(MenuItem menuItem)
        {
            string result = string.Empty;

            switch (menuItem.Level)
            {
                case 1:
                    var dbList = await client.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(menuItem.Title)).ConfigureAwait(false);
                    result = dbList.Resource.ToString();
                    break;
                case 2:
                    var collectionList = await client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(menuItem.DbName, menuItem.Title)).ConfigureAwait(false);
                    result = collectionList.Resource.ToString();
                    break;
                case 3:
                    result = GetDocumentInfo(menuItem, $"WHERE c.id = '{menuItem.Title}'");
                    break;
                default:
                    break;
            }
            return result;
        }

        /// <summary>
        /// The DeleteItems
        /// </summary>
        /// <param name="documentItem">The documentItem</param>
        /// <param name="query">The query</param>
        /// <returns></returns>
        public async Task DeleteItems(MenuItem documentItem, string query)
        {
            try
            {
                var docCollection = await client.ReadDocumentCollectionAsync(documentItem.CollectionSelfLink).ConfigureAwait(false);

                var result = docCollection.Resource.ToString();

                var partitionKeys = ParsePartitionKey(result);

                if (partitionKeys.Any())
                {
                    foreach (Document document in client.CreateDocumentQuery<Document>(documentItem.CollectionSelfLink, $"SELECT * FROM c {query}", new FeedOptions { PartitionKey = new PartitionKey(Undefined.Value) }))
                    {
                        await client.DeleteDocumentAsync(document.SelfLink, new RequestOptions { PartitionKey = new PartitionKey(Undefined.Value) });
                    }

                    foreach (var partitionKey in partitionKeys)
                    {
                        foreach (var docItem in client.CreateDocumentQuery<Document>(documentItem.CollectionSelfLink, $"SELECT * FROM c {query}", new FeedOptions { PartitionKey = new PartitionKey(partitionKey) /*EnableCrossPartitionQuery = true*/ }))
                        {
                            await client.DeleteDocumentAsync(docItem.SelfLink, new RequestOptions { PartitionKey = new PartitionKey(partitionKey) }).ConfigureAwait(false);
                        }
                    }
                }
                else
                {
                    foreach (var docItem in client.CreateDocumentQuery<Document>(documentItem.CollectionSelfLink, $"SELECT * FROM c {query}", new FeedOptions { EnableCrossPartitionQuery = true }))
                    {
                        await client.DeleteDocumentAsync(docItem.SelfLink).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// The ParsePartitionKey
        /// </summary>
        /// <param name="docCollection">The docCollection</param>
        /// <returns>The List of PartitionKeys</returns>
        private List<string> ParsePartitionKey(string docCollection)
        {
            string partitionKey = string.Empty;
            List<string> resultPartitionKeys = new List<string>();
            try
            {
                if (!string.IsNullOrWhiteSpace(docCollection))
                {
                    var jObject = JObject.Parse(docCollection);

                    JToken token = jObject.SelectToken("partitionKey.paths");
                    if (token.Type == JTokenType.Array)
                    {
                        var items = (JArray)token;
                        partitionKey = string.Join(",", items).Replace('/', ' ').Trim();
                    }
                    resultPartitionKeys = partitionKey.Trim().Split(',').ToList();
                }
            }
            catch (Exception ex)
            {
            }
            return resultPartitionKeys;
        }

        /// <summary>
        /// The GetDocumentInfo
        /// </summary>
        /// <param name="menuItem">The menuItem</param>
        /// <param name="query">The query</param>
        /// <returns>The result string</returns>
        public string GetDocumentInfo(MenuItem menuItem, string query)
        {
            string result = string.Empty;
            try
            {
                List<string> docItems = new List<string>();
                foreach (var documentList in client.CreateDocumentQuery(menuItem.CollectionSelfLink, $"SELECT * FROM c {query}", new FeedOptions { EnableCrossPartitionQuery = true }).ToList())
                {
                    docItems.Add(JObject.Parse(documentList?.ToString()).ToString());
                }
                result = docItems.Count > 0 ? $"[{string.Join(",", docItems)}]" : string.Empty;
            }
            catch (Exception)
            {
                return "Nothing to display";
            }

            return result;
        }
    }

    /// <summary>
    /// The MenuItem
    /// </summary>
    public class MenuItem
    {
        public MenuItem()
        {
            this.Items = new ObservableCollection<MenuItem>();
        }

        public string Title { get; set; }

        public int Level { get; set; }

        public string DbName { get; set; }

        public string CollectioName { get; set; }

        public string DocSelfLink { get; set; }

        public string CollectionSelfLink { get; set; }

        public string Secret { get; set; }

        public string Endpoint { get; set; }
        public ObservableCollection<MenuItem> Items { get; set; }
    }

}
