﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Namotion.Messaging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Namotion.Messaging.RabbitMQ
{
    public class RabbitMessageReceiver : IMessageReceiver
    {
        private const string DeliveryTagProperty = "DeliveryTag";

        private readonly RabbitConfiguration _configuration;

        private IModel _channel;

        public RabbitMessageReceiver(RabbitConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <inheritdoc/>
        public async Task ListenAsync(Func<IReadOnlyCollection<Message>, CancellationToken, Task> handleMessages, CancellationToken cancellationToken = default)
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration.Host,
                UserName = _configuration.Username,
                Password = _configuration.Password,
                DispatchConsumersAsync = true
            };

            using (var connection = factory.CreateConnection())
            using (_channel = connection.CreateModel())
            {
                _channel.ExchangeDeclare(_configuration.ExchangeName, ExchangeType.Direct);
                _channel.QueueDeclare(_configuration.QueueName, true, false, false, null);
                _channel.QueueBind(_configuration.QueueName, _configuration.ExchangeName, _configuration.Routingkey, null);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.Received += async (o, a) =>
                {
                    var message = new Message(
                        id: a.BasicProperties.MessageId,
                        content: a.Body,
                        systemProperties: new Dictionary<string, object>
                        {
                            { DeliveryTagProperty, a.DeliveryTag }
                        }
                    );

                    try
                    {
                        await handleMessages(new Message[] { message }, cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        await RejectAsync(message, cancellationToken).ConfigureAwait(false);
                    }
                };

                _channel.BasicConsume(_configuration.QueueName, _configuration.AutoAck, consumer);
                await Task.Delay(Timeout.Infinite, cancellationToken);
                _channel = null;
            }
        }

        /// <inheritdoc/>
        public Task<long> GetMessageCountAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<long>(_channel.MessageCount(_configuration.QueueName));
        }

        /// <inheritdoc/>
        public Task ConfirmAsync(IEnumerable<Message> messages, CancellationToken cancellationToken = default)
        {
            _ = _channel ?? throw new InvalidOperationException("Queue is not in listening mode.");

            foreach (var message in messages)
            {
                _channel.BasicAck((ulong)message.SystemProperties[DeliveryTagProperty], true);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task RejectAsync(Message message, CancellationToken cancellationToken = default)
        {
            _ = _channel ?? throw new InvalidOperationException("Queue is not in listening mode.");

            _channel.BasicReject((ulong)message.Properties[DeliveryTagProperty], true);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException" />
        public Task DeadLetterAsync(Message message, string reason, string errorDescription, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException" />
        public Task KeepAliveAsync(Message message, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}