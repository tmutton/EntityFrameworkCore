﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNet.Logging;
using Microsoft.Data.Entity.ChangeTracking;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Query;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Relational.Update;
using Microsoft.Data.Relational.Utilities;
using Remotion.Linq;

namespace Microsoft.Data.Relational
{
    public abstract partial class RelationalDataStore : DataStore
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;

        protected RelationalDataStore([NotNull] string connectionString, [NotNull] ILogger logger)
        {
            Check.NotEmpty(connectionString, "connectionString");
            Check.NotNull(logger, "logger");

            _connectionString = connectionString;
            _logger = logger;
        }

        public abstract DbConnection CreateConnection([NotNull] string connectionString);

        public virtual DbConnection CreateConnection()
        {
            return CreateConnection(_connectionString);
        }

        protected abstract SqlGenerator SqlGenerator { get; }

        public virtual string ConnectionString
        {
            get { return _connectionString; }
        }

        public ILogger Logger
        {
            get { return _logger; }
        }

        public override async Task<int> SaveChangesAsync(
            IEnumerable<StateEntry> stateEntries,
            IModel model,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Check.NotNull(stateEntries, "stateEntries");
            Check.NotNull(model, "model");

            //TODO: this should be cached
            var database = new DatabaseBuilder().Build(model);

            var commands = new CommandBatchPreparer().BatchCommands(stateEntries, database);

            using (var connection = CreateConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken);

                var executor = new BatchExecutor(commands, SqlGenerator);
                await executor.ExecuteAsync(connection, cancellationToken);
            }

            // TODO Return the actual results once we can get them
            return stateEntries.Count();
        }

        public override IAsyncEnumerable<TResult> Query<TResult>(
            QueryModel queryModel, IModel model, StateManager stateManager)
        {
            Check.NotNull(queryModel, "queryModel");
            Check.NotNull(model, "model");
            Check.NotNull(stateManager, "stateManager");

            var queryModelVisitor = new QueryModelVisitor();
            var queryExecutor = queryModelVisitor.CreateQueryExecutor<TResult>(queryModel);
            var queryContext = new RelationalQueryContext(model, stateManager, this);

            // TODO: Need async in query compiler
            return new CompletedAsyncEnumerable<TResult>(queryExecutor(queryContext));
        }
    }
}
