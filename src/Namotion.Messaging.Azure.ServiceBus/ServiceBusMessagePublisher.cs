﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Namotion.Messaging.Abstractions;

namespace Namotion.Messaging.Azure.ServiceBus
{
    /// <summary>
    /// A Service Bus message publisher.
    /// </summary>
    public class ServiceBusMessagePublisher : IMessagePublisher
    {
        private readonly QueueClient _client;

        private ServiceBusMessagePublisher(QueueClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Creates a new Service Bus publisher from a queue client.
        /// </summary>
        /// <param name="queueClient">The queue client.</param>
        /// <returns>The message publisher.</returns>
        public static IMessagePublisher CreateFromQueueClient(QueueClient queueClient)
        {
            return new ServiceBusMessagePublisher(queueClient);
        }

        /// <summary>
        /// Creates a new Service Bus publisher from a connection string.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="entityPath">The entity path.</param>
        /// <returns>The message publisher.</returns>
        public static IMessagePublisher Create(string connectionString, string entityPath)
        {
            return new ServiceBusMessagePublisher(new QueueClient(connectionString, entityPath));
        }

        /// <inheritdoc/>
        public async Task SendAsync(IEnumerable<Abstractions.Message> messages, CancellationToken cancellationToken = default)
        {
            await _client.SendAsync(messages.Select(m => CreateMessage(m)).ToList());
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _client.CloseAsync().GetAwaiter().GetResult();
        }

        private Microsoft.Azure.ServiceBus.Message CreateMessage(Abstractions.Message message)
        {
            var abstractMessage = new Microsoft.Azure.ServiceBus.Message(message.Content)
            {
                MessageId = message.Id ?? Guid.NewGuid().ToString()
            };

            foreach (var property in message.Properties)
            {
                abstractMessage.UserProperties[property.Key] = property.Value;
            }

            return abstractMessage;
        }
    }
}