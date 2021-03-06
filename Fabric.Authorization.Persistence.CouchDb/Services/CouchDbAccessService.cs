﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Fabric.Authorization.Domain.Exceptions;
using Fabric.Authorization.Domain.Models;
using Fabric.Authorization.Persistence.CouchDb.Configuration;
using Fabric.Authorization.Persistence.CouchDb.Stores;
using MyCouch;
using MyCouch.Net;
using MyCouch.Requests;
using MyCouch.Responses;
using Newtonsoft.Json;
using Serilog;

namespace Fabric.Authorization.Persistence.CouchDb.Services
{
    public class CouchDbAccessService : IDocumentDbService
    {
        private readonly ILogger _logger;
        private readonly ICouchDbSettings _couchDbSettings;
        private const string HighestUnicodeChar = "\ufff0";

        private string GetFullDocumentId<T>(string documentId)
        {
            return DocumentDbHelpers.GetFullDocumentId<T>(documentId);
        }

        private DbConnectionInfo DbConnectionInfo
        {
            get
            {
                var connectionInfo = new DbConnectionInfo(_couchDbSettings.Server, _couchDbSettings.DatabaseName);

                if (!string.IsNullOrEmpty(_couchDbSettings.Username) &&
                    !string.IsNullOrEmpty(_couchDbSettings.Password))
                {
                    connectionInfo.BasicAuth =
                        new BasicAuthString(_couchDbSettings.Username, _couchDbSettings.Password);
                }

                return connectionInfo;
            }
        }

        public CouchDbAccessService(ICouchDbSettings config, ILogger logger)
        {
            _couchDbSettings = config;
            _logger = logger;

            _logger.Debug(
                $"couchDb configuration properties: Server: {config.Server} -- DatabaseName: {config.DatabaseName}");            
        }

        public async Task Initialize()
        {
            using (var client = new MyCouchClient(DbConnectionInfo))
            {
                if (!(await client.Database.GetAsync()).IsSuccess)
                {
                    _logger.Information($"could not retrieve database information. attempting to create. Current Thread Id: {Thread.CurrentThread.ManagedThreadId}");
                    var creation = await client.Database.PutAsync();
                    _logger.Information($"database created if it did not exist. Current Thread Id: {Thread.CurrentThread.ManagedThreadId}");
                    if (!creation.IsSuccess)
                    {
                        throw new ArgumentException(creation.Error);
                    }
                }
            }
        }

        public async Task SetupDefaultUser()
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri(_couchDbSettings.Server);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(HttpContentTypes.Json));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", DbConnectionInfo.BasicAuth.Value);
                var response = await httpClient.PutAsync($"{_couchDbSettings.DatabaseName}/_security",
                    GetCouchDbUserPayload());

                if (!response.IsSuccessStatusCode)
                {
                    throw new CouldNotCompleteOperationException($"unable to create the {_couchDbSettings.DatabaseName}_user in {_couchDbSettings.DatabaseName}, response: {response.Content.ReadAsStringAsync().Result}, responseStatusCode: {response.StatusCode}.");
                }
            }
        }

        public async Task<T> GetDocument<T>(string documentId)
        {
            using (var client = new MyCouchClient(DbConnectionInfo))
            {
                var documentResponse = await client.Documents.GetAsync(GetFullDocumentId<T>(documentId));

                if (!documentResponse.IsSuccess)
                {
                    _logger.Debug($"unable to find {typeof(T).Name} entity: {GetFullDocumentId<T>(documentId)} - message: {documentResponse.Reason}");
                    return default(T);
                }

                var json = documentResponse.Content;
                var document = JsonConvert.DeserializeObject<T>(json);

                return document;
            }
        }

        public async Task<IEnumerable<T>> GetDocuments<T>(string documentType)
        {
            using (var client = new MyCouchClient(DbConnectionInfo))
            {
                var viewQuery = new QueryViewRequest(SystemViewIdentity.AllDocs)
                    .Configure(q => q.Reduce(false)
                        .IncludeDocs(true)
                        .StartKey(documentType)
                        .EndKey($"{documentType}{HighestUnicodeChar}"));

                var result = await client.Views.QueryAsync(viewQuery);

                if (!result.IsSuccess)
                {
                    _logger.Error($"unable to find documents for type: {documentType} - error: {result.Reason}");
                    return default(IEnumerable<T>);
                }

                var results = new List<T>();

                foreach (var responseRow in result.Rows)
                {                    
                    var resultRow = JsonConvert.DeserializeObject<T>(responseRow.IncludedDoc);
                    if (resultRow is ISoftDelete && (resultRow as ISoftDelete).IsDeleted)
                    {
                        continue;
                    }
                    results.Add(resultRow);
                }

                return results;
            }
        }

        public async Task<int> GetDocumentCount(string documentType)
        {
            using (var client = new MyCouchClient(DbConnectionInfo))
            {
                var viewQuery = new QueryViewRequest("TODO", documentType);
                var result = await client.Views.QueryAsync<int>(viewQuery);
                if (result.Rows != null && result.Rows.Any())
                {
                    return result.Rows.First().Value;
                }

                return -1;
            }
        }

        public async Task AddDocument<T>(string documentId, T documentObject)
        {
            var fullDocumentId = GetFullDocumentId<T>(documentId);

            using (var client = new MyCouchClient(DbConnectionInfo))
            {
                var existingDoc = await client.Documents.GetAsync(fullDocumentId);
                var docJson = JsonConvert.SerializeObject(documentObject);

                if (!string.IsNullOrEmpty(existingDoc.Id))
                {
                    throw new AlreadyExistsException<T>($"{typeof(T).Name} entity with id {documentId} already exists.");
                }

                var response = await client.Documents.PutAsync(fullDocumentId, docJson);

                if (!response.IsSuccess)
                {
                    _logger.Error($"unable to add or update {typeof(T).Name} entity: {documentId} - error: {response.Reason}");
                }
            }
        }

        public async Task UpdateDocument<T>(string documentId, T documentObject)
        {
            var fullDocumentId = GetFullDocumentId<T>(documentId);

            using (var client = new MyCouchClient(DbConnectionInfo))
            {
                var existingDoc = await client.Documents.GetAsync(fullDocumentId);
                var docJson = JsonConvert.SerializeObject(documentObject);

                if (existingDoc.IsEmpty)
                {
                    throw new ArgumentException($"{typeof(T).Name} entity with id {documentId} does not exist.");
                }

                var response = await client.Documents.PutAsync(fullDocumentId, existingDoc.Rev, docJson);

                if (!response.IsSuccess)
                {
                    _logger.Error($"unable to add or update document: {documentId} - error: {response.Reason}");
                }
            }
        }

        public async Task DeleteDocument<T>(string documentId)
        {
            using (var client = new MyCouchClient(DbConnectionInfo))
            {
                var documentResponse = await client.Documents.GetAsync(GetFullDocumentId<T>(documentId));
                await Delete(documentResponse.Id, documentResponse.Rev);
            }
        }

        private async Task Delete(string documentId, string rev)
        {
            using (var client = new MyCouchClient(DbConnectionInfo))
            {
                var response = await client.Documents.DeleteAsync(documentId, rev);

                if (!response.IsSuccess)
                {
                    _logger.Error($"There was an error deleting document:{documentId}, error: {response.Reason}");
                }
            }
        }

        public async Task AddViews(string documentId, CouchDbViews views)
        {
            var fullDocumentId = $"_design/{documentId}";

            using (var client = new MyCouchClient(DbConnectionInfo))
            {
                var existingDoc = await client.Documents.GetAsync(fullDocumentId);
                var docJson = JsonConvert.SerializeObject(views);
                DocumentHeaderResponse response;

                if (!string.IsNullOrEmpty(existingDoc.Id))
                {
                    response = await client.Documents.PutAsync(fullDocumentId, existingDoc.Rev, docJson);
                }
                else
                {
                    response = await client.Documents.PutAsync(fullDocumentId, docJson);
                }

                if (!response.IsSuccess)
                {
                    _logger.Error($"unable to add or update document: {documentId} - error: {response.Reason}");
                    throw new CouldNotCompleteOperationException($"unable to add view: {documentId} - error: {response.Reason}");
                }
            }
        }

        public async Task<IEnumerable<T>> GetDocuments<T>(string designdoc, string viewName, Dictionary<string, object> customParams)
        {
            using (var client = new MyCouchClient(DbConnectionInfo))
            {
                var viewQuery = new QueryViewRequest(designdoc, viewName);
                viewQuery.CustomQueryParameters = customParams;
                ViewQueryResponse result = await client.Views.QueryAsync(viewQuery);

                if (!result.IsSuccess)
                {
                    _logger.Error($"unable to execute view: {viewName} - error: {result.Reason}");
                    return new List<T>();
                }

                var results = new List<T>();

                foreach (var responseRow in result.Rows)
                {
                    var resultRow = JsonConvert.DeserializeObject<T>(responseRow.IncludedDoc);
                    if (resultRow is ISoftDelete && (resultRow as ISoftDelete).IsDeleted)
                    {
                        continue;
                    }
                    results.Add(resultRow);
                }

                return results;
            }
        }

        public async Task<IEnumerable<T>> GetDocuments<T>(string designdoc, string viewName, string key)
        {
            using (var client = new MyCouchClient(DbConnectionInfo))
            {
                var viewQuery = new QueryViewRequest(designdoc, viewName);
                var result = await client.Views.QueryAsync(viewQuery);

                if (!result.IsSuccess)
                {
                    _logger.Error($"unable to execute view: {viewName} - error: {result.Reason}");
                    return new List<T>();
                }

                var results = new List<T>();

                foreach (var responseRow in result.Rows)
                {
                    if (responseRow.Key != null && (string.IsNullOrEmpty(key) || responseRow.Key.ToString() == key))
                    {
                        var resultRow = JsonConvert.DeserializeObject<T>(responseRow.Value);
                        if (resultRow is ISoftDelete && (resultRow as ISoftDelete).IsDeleted)
                        {
                            continue;
                        }
                        results.Add(resultRow);
                    }
                }

                return results;
            }
        }
        private JsonContent GetCouchDbUserPayload()
        {
            return new JsonContent(
                $"{{\"admins\": {{ \"names\": [], \"roles\": [] }}, \"members\": {{ \"names\": [\"{_couchDbSettings.DatabaseName}_user\"], \"roles\": [] }} }}");
        }
    }
}