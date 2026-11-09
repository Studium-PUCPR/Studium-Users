using Amazon;
using Studium_Users.Services.BookingEventsHandlers;
using Studium_Users.Services.DTOs;
using Wolverine;
using Wolverine.AmazonSqs;

namespace Studium_Users.Api.Extensions;

public static class MessagingExtensions
{
    public static void AddStudiumMessaging(this WebApplicationBuilder builder)
    {
        builder.Host.UseWolverine(opts =>
        {
            opts.Discovery.IncludeAssembly(typeof(BookingEventsHandler).Assembly);

            var awsRegion = builder.Configuration["AWS:Region"] ?? "us-east-2";
            opts.UseAmazonSqsTransport(cfg =>
            {
                cfg.RegionEndpoint = RegionEndpoint.GetBySystemName(awsRegion);
                var serviceUrl = builder.Configuration["AWS:ServiceURL"];
                if (!string.IsNullOrWhiteSpace(serviceUrl))
                    cfg.ServiceURL = serviceUrl;
            }).AutoProvision();

            var usersQueueName = builder.Configuration["AWS:SQS:UsersQueueName"];
            if (!string.IsNullOrWhiteSpace(usersQueueName))
            {
                opts.ListenToSqsQueue(usersQueueName);
            }

            var bookingQueueName = builder.Configuration["AWS:SQS:BookingQueueName"];
            if (!string.IsNullOrWhiteSpace(bookingQueueName))
            {
                opts.PublishMessage<UserStatusChangedEvent>().ToSqsQueue(bookingQueueName);
            }
        });
    }
}
