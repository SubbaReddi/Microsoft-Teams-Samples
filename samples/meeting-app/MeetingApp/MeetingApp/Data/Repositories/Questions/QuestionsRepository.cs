﻿using MeetingApp.Data.Models;
using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MeetingApp.Data.Repositories.Questions
{
    public class QuestionsRepository: IQuestionsRepository
    {
        private readonly Lazy<Task> initializeTask;
        private CloudTable questionCloudTable;

        /// <summary>
        /// Initializes a new instance of the <see cref="QuestionsRepository"/> class.
        /// </summary>
        /// <param name="connectionString">connection string of storage provided by dependency injection.</param>
        public QuestionsRepository(string connectionString)
        {
            this.initializeTask = new Lazy<Task>(() => this.InitializeTableStorageAsync(connectionString));
        }

        /// <summary>
        /// Create CandidateDetails table if it doesn't exist.
        /// </summary>
        /// <param name="connectionString">storage account connection string.</param>
        /// <returns><see cref="Task"/> representing the asynchronous operation task which represents table is created if its not existing.</returns>
        private async Task InitializeTableStorageAsync(string connectionString)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            CloudTableClient cloudTableClient = storageAccount.CreateCloudTableClient();
            this.questionCloudTable = cloudTableClient.GetTableReference("Questions");

            await this.questionCloudTable.CreateIfNotExistsAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Store or update questions in table storage.
        /// </summary>
        /// <param name="entity">Represents questionSet entity used for storage and retrieval.</param>
        /// <returns><see cref="Task"/> that represents configuration entity is saved or updated.</returns>
        public async Task<TableResult> StoreOrUpdateQuestionEntityAsync(QuestionSetEntity entity)
        {
            entity.PartitionKey = entity.MeetingId;
            entity.RowKey = entity.QuestionId == null ? string.Format("{0:D19}", DateTime.UtcNow.Ticks) : entity.QuestionId;
            await this.EnsureInitializedAsync().ConfigureAwait(false);
            TableOperation addOrUpdateOperation = TableOperation.InsertOrReplace(entity);
            return await this.questionCloudTable.ExecuteAsync(addOrUpdateOperation).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a particular question in table storage.
        /// </summary>
        /// <returns><see cref="Task"/> Already saved entity detail.</returns>
        public async Task<int> DeleteQuestion(QuestionSetEntity entity)
        {
            await this.EnsureInitializedAsync().ConfigureAwait(false);
            entity.PartitionKey = entity.MeetingId;
            entity.RowKey = entity.QuestionId;
            entity.ETag = "*";
            var deleteOperation = TableOperation.Delete(entity);
            var result = await this.questionCloudTable.ExecuteAsync(deleteOperation).ConfigureAwait(false);

            return (int)result.HttpStatusCode;
        }

        /// <summary>
        /// Edit a particular question in table storage.
        /// </summary>
        /// <returns><see cref="Task"/> Already saved entity detail.</returns>
        public async Task<IEnumerable<QuestionSetEntity>> GetQuestions(string meetingId)
        {
            await this.EnsureInitializedAsync().ConfigureAwait(false);

            TableContinuationToken continuationToken = null;
            TableQuery<QuestionSetEntity> query = new TableQuery<QuestionSetEntity>()
            .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, meetingId));
            var searchResult = await this.questionCloudTable.ExecuteQuerySegmentedAsync(query, continuationToken).ConfigureAwait(false);

            return searchResult.ToList();
        }

        /// <summary>
        /// Initialization of InitializeAsync method which will help in creating table.
        /// </summary>
        /// <returns>Represent a task with initialized connection data.</returns>
        private async Task EnsureInitializedAsync()
        {
            await this.initializeTask.Value.ConfigureAwait(false);
        }
    }
}
