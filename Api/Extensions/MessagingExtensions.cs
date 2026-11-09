using Studium_Users.Services.BookingEventsHandlers;
using Studium_Users.Services.DTOs;
using Wolverine;
using Wolverine.RabbitMQ;

namespace Studium_Users.Api.Extensions;

public static class MessagingExtensions
{
    public static void AddStudiumMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            opts.Discovery.IncludeAssembly(typeof(BookingEventsHandler).Assembly);

            var rabbitConnectionString = builder.Configuration["RabbitMQ:ConnectionString"]
                ?? builder.Configuration["RABBITMQ_URL"]
                ?? "amqp://studium:StudiumRabbitMQ2026!@studium-rabbitmq-service.studium.svc.cluster.local:5672";

            opts.UseRabbitMq(new Uri(rabbitConnectionString)).AutoProvision();

            var usersQueueName = builder.Configuration["RabbitMQ:UsersQueueName"] ?? "studium-users-events-queue";
            opts.ListenToRabbitQueue(usersQueueName);

            var bookingQueueName = builder.Configuration["RabbitMQ:BookingQueueName"] ?? "studium-booking-events-queue";
            opts.PublishMessage<UserStatusChangedEvent>().ToRabbitQueue(bookingQueueName);

            var notificationsQueueName = builder.Configuration["RabbitMQ:NotificationsQueueName"] ?? "studium-notifications-queue";
            opts.PublishMessage<UserStatusChangedEvent>().ToRabbitQueue(notificationsQueueName);
        });
    }
}
