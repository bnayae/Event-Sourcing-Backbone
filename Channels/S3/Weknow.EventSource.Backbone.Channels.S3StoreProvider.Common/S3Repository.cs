﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.Extensions.Logging;

using static Weknow.EventSource.Backbone.EventSourceConstants;

namespace Weknow.EventSource.Backbone.Channels
{

    /// <summary>
    /// Abstract S3 operations
    /// </summary>
    public sealed class S3Repository : IS3Repository, IDisposable
    {
        const string BUCKET_KEY = "S3_EVENT_SOURCE_BUCKET";
        private static readonly string BUCKET = Environment.GetEnvironmentVariable(BUCKET_KEY) ?? string.Empty;

        private readonly string _bucket;
        private readonly ILogger _logger;
        private readonly string? _basePath;
        private readonly AmazonS3Client _client;
        private static readonly List<Tag> EMPTY_TAGS = new List<Tag>();
        private int _disposeCount = 0;
        private readonly S3EnvironmentConvention _environmentConvension;

        #region Ctor

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="client">S3 client.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="options">The s3 options.</param>
        public S3Repository(
                    AmazonS3Client client,
                    ILogger logger,
                    S3Options options = default)
        {
            _client = client;
            _logger = logger;
            _bucket = options.Bucket ?? BUCKET;
            _basePath = options.BasePath;
            _environmentConvension = options.EnvironmentConvension;
        }

        #endregion // Ctor

        #region AddReference

        /// <summary>
        /// Adds the reference to the repository.
        /// This reference will prevent disposal until having no active references.
        /// </summary>
        internal void AddReference() => Interlocked.Increment(ref _disposeCount);

        #endregion // AddReference

        #region GetJsonAsync

        /// <summary>
        /// Get content.
        /// </summary>
        /// <param name="env">Environment</param>
        /// <param name="id">The identifier which is the S3 key.</param>
        /// <param name="cancellation">The cancellation.</param>
        /// <returns></returns>
        /// <exception cref="System.IO.InvalidDataException"></exception>
        public async ValueTask<JsonElement> GetJsonAsync(string env, string id, CancellationToken cancellation = default)
        {
            try
            {
                Stream srm = await GetStreamAsync(env, id, cancellation);

                var response = await JsonDocument.ParseAsync(srm);
                return response.RootElement;
            }
            #region Exception Handling

            catch (Exception e)
            {
                string msg = "S3 Failed to parse json:";
                _logger.LogError(e.FormatLazy(), msg);
                throw new InvalidDataException();
            }

            #endregion // Exception Handling
        }

        #endregion // GetJsonAsync

        #region GetJsonAsync

        /// <summary>
        /// Get content.
        /// </summary>
        /// <param name="env">Environment</param>
        /// <param name="id">The identifier which is the S3 key.</param>
        /// <param name="cancellation">The cancellation.</param>
        /// <returns></returns>
        /// <exception cref="System.IO.InvalidDataException"></exception>
        public async ValueTask<byte[]> GetBytesAsync(string env,  string id, CancellationToken cancellation = default)
        {
            try
            {
                Stream srm = await GetStreamAsync(env, id, cancellation);
                var buffer = new byte[srm.Length];
                await srm.ReadAsync(buffer, cancellation);
                return buffer;
            }
            #region Exception Handling

            catch (Exception e)
            {
                string msg = "S3 Failed to parse json:";
                _logger.LogError(e.FormatLazy(), msg);
                throw new InvalidDataException();
            }

            #endregion // Exception Handling
        }

        #endregion // GetJsonAsync

        #region GetAsync


        /// <summary>
        /// Get content.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="env">Environment</param>
        /// <param name="id">The identifier which is the S3 key.</param>
        /// <param name="cancellation">The cancellation.</param>
        /// <returns></returns>
        /// <exception cref="System.NullReferenceException">Failed to deserialize industries</exception>
        /// <exception cref="System.IO.InvalidDataException"></exception>
        public async ValueTask<T> GetAsync<T>(string env, string id, CancellationToken cancellation = default)
        {
            try
            {
                Stream srm = await GetStreamAsync(env, id, cancellation);

                var response = await JsonSerializer.DeserializeAsync<T>(srm, SerializerOptionsWithIndent);

                #region Validation

                if (response == null)
                {
                    throw new NullReferenceException("Failed to deserialize industries");
                }

                #endregion // Validation
                return response;
            }
            #region Exception Handling

            catch (Exception e)
            {
                string msg = $"S3 Failed to deserialize into [{typeof(T).Name}]:";
                _logger.LogError(e.FormatLazy(), msg);
                throw new InvalidDataException();
            }

            #endregion // Exception Handling
        }

        #endregion // GetAsync

        #region GetStreamAsync

        /// <summary>
        /// Get content.
        /// </summary>
        /// <param name="env">environment</param>
        /// <param name="id">The identifier which is the S3 key.</param>
        /// <param name="cancellation">The cancellation.</param>
        /// <returns></returns>
        /// <exception cref="GetObjectRequest">
        /// </exception>
        /// <exception cref="System.Exception">Failed to get blob [{res.HttpStatusCode}]</exception>
        public async ValueTask<Stream> GetStreamAsync(string env, string id, CancellationToken cancellation = default)
        {
            try
            {
                string key = GetKey(env, id);
                var s3Request = new GetObjectRequest
                {
                    BucketName = GetBucket(env),
                    Key = key
                };
                // s3Request.Headers.ExpiresUtc = DateTime.Now.AddHours(2); // cache expiration

                GetObjectResponse res = await _client.GetObjectAsync(s3Request, cancellation);

                #region Validation

                if (res.HttpStatusCode >= HttpStatusCode.Ambiguous)
                {
                    throw new Exception($"Failed to get blob [{res.HttpStatusCode}]");
                }

                #endregion // Validation

                return res.ResponseStream;
            }
            #region Exception Handling

            catch (AmazonS3Exception e)
            {
                _logger.LogError(e.FormatLazy(),
                        "S3 Failed to get:");
                throw;
            }
            catch (Exception e)
            {
                _logger.LogError(e.FormatLazy(),
                        "S3 get Failed");
                throw;
            }

            #endregion // Exception Handling
        }

        #endregion // GetStreamAsync

        #region SaveAsync

        /// <summary>
        /// Saves content.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="env">Environment</param>
        /// <param name="id">The identifier of the resource.</param>
        /// <param name="metadata">The metadata.</param>
        /// <param name="tags">The tags.</param>
        /// <param name="cancellation">The cancellation.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Failed to save blob [{res.HttpStatusCode}]</exception>
        public async ValueTask<BlobResponse> SaveAsync(
            JsonElement data, 
            string env,
            string id,
            IImmutableDictionary<string, string>? metadata = null,
            IImmutableDictionary<string, string>? tags = null,
            CancellationToken cancellation = default)
        {
            var result = await SaveAsync(data.ToStream(), env, id, metadata, tags, "application/json", cancellation);
            return result;
        }

        /// <summary>
        /// Saves content.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="env">Environment</param>
        /// <param name="id">The identifier of the resource.</param>
        /// <param name="metadata">The metadata.</param>
        /// <param name="tags">The tags.</param>
        /// <param name="mediaType">Type of the media.</param>
        /// <param name="cancellation">The cancellation.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Failed to save blob [{res.HttpStatusCode}]</exception>
        public async ValueTask<BlobResponse> SaveAsync(
            ReadOnlyMemory<byte> data,
            string env,
            string id,
            IImmutableDictionary<string, string>? metadata = null,
            IImmutableDictionary<string, string>? tags = null,
            string? mediaType = null,
            CancellationToken cancellation = default)
        {
            using var srm = new MemoryStream(data.ToArray()); // TODO: [bnaya 2021-07] consider AsStream -> https://www.nuget.org/packages/Microsoft.Toolkit.HighPerformance
            var result = await SaveAsync(srm, env, id, metadata, tags, mediaType, cancellation);
            return result;
        }

        /// <summary>
        /// Saves content.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="env">Environment</param>
        /// <param name="id">The identifier of the resource.</param>
        /// <param name="metadata">The metadata.</param>
        /// <param name="tags">The tags.</param>
        /// <param name="mediaType">Type of the media.</param>
        /// <param name="cancellation">The cancellation.</param>
        /// <returns></returns>
        /// <exception cref="System.Exception">Failed to save blob [{res.HttpStatusCode}]</exception>
        /// <exception cref="Exception">Failed to save blob [{res.HttpStatusCode}]</exception>
        public async ValueTask<BlobResponse> SaveAsync(
            Stream data,
            string env,
            string id,
            IImmutableDictionary<string, string>? metadata = null,
            IImmutableDictionary<string, string>? tags = null,
            string? mediaType = null,
            CancellationToken cancellation = default)
        {
            try
            {
                var date = DateTime.UtcNow;
                string key = GetKey(env, id);
                //tags = tags.Add("month", date.ToString("yyyy-MM"));

                var s3Request = new PutObjectRequest
                {
                    BucketName = GetBucket(env),
                    Key = key,
                    InputStream = data,
                    ContentType = mediaType,
                    TagSet = tags?.Select(m => new Tag { Key = m.Key, Value = m.Value })?.ToList() ?? EMPTY_TAGS,
                };
                // s3Request.Headers.ExpiresUtc = DateTime.Now.AddHours(2); // cache expiration

                if (metadata != null)
                {
                    foreach (var meta in metadata)
                    {
                        s3Request.Metadata.Add(meta.Key, meta.Value);
                    }
                }

                PutObjectResponse res = await _client.PutObjectAsync(s3Request, cancellation);

                #region Validation

                if (res.HttpStatusCode >= HttpStatusCode.Ambiguous)
                {
                    throw new Exception($"Failed to save blob [{res.HttpStatusCode}]");
                }

                #endregion // Validation

                var response = new BlobResponse(key, _bucket, res.ETag, res.VersionId);
                return response;
            }
            #region Exception Handling

            catch (AmazonS3Exception e)
            {
                string json = "";
                try
                {
                    json = data.Serialize();
                }
                catch { }
                _logger.LogError(e.FormatLazy(),
                        "AWS-S3 Failed to write: {payload}", json);
                throw;
            }
            catch (Exception e)
            {
                string json = "";
                try
                {
                    json = data.Serialize();
                }
                catch { }
                _logger.LogError(e.FormatLazy(),
                        "S3 writing Failed: {payload}", json);
                throw;
            }

            #endregion // Exception Handling
        }

        #endregion // SaveAsync

        #region GetBucket

        /// <summary>
        /// Get the Bucket name
        /// </summary>
        /// <param name="env">Environment</param>
        /// <returns></returns>
        private string GetBucket(string env)
        {
            var bucket = _environmentConvension switch
            {
                S3EnvironmentConvention.BucketPrefix => $"{env}.{_bucket}",
                _ => _bucket
            };
            return bucket;
        }

        #endregion // GetBucket

        #region GetKey

        /// <summary>
        /// Gets s3 key.
        /// </summary>
        /// <param name="env">Environment</param>
        /// <param name="id">The identifier.</param>
        /// <returns></returns>
        private string GetKey(string env, string id)
        {
            string sep = string.IsNullOrEmpty(_basePath) ? string.Empty : "/";
            string key = $"{_basePath}{sep}{Uri.UnescapeDataString(id)}";
            if (_environmentConvension == S3EnvironmentConvention.PathPrefix)
                key = $"{env}/{key}";
            return key;
        }

        #endregion // GetKey

        #region Dispose Pattern

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Decrement(ref _disposeCount) > 0) return;
            _client.Dispose();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="S3RepositoryFactory"/> class.
        /// </summary>
        ~S3Repository()
        {
            Dispose(false);
        }

        #endregion // Dispose Pattern
    }
}
