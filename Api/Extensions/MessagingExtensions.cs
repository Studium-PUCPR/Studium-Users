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

            var awsRegion = builder.Configuration["AWS:Region"] ?? "us-east-1";
            opts.UseAmazonSqsTransport(cfg =>
            {
                cfg.RegionEndpoint = RegionEndpoint.GetBySystemName(awsRegion);
                var serviceUrl = builder.Configuration["AWS:ServiceURL"];
                if (!string.IsNullOrWhiteSpace(serviceUrl))
                    cfg.ServiceURL = serviceUrl;
            });

            var usersQueueName = builder.Configuration["AWS:SQS:UsersQueueName"];
            if (!string.IsNullOrWhiteSpace(usersQueueName))
            {
                opts.ListenToSqsQueue(usersQueueName);
            }

            var outboundQueues = builder.Configuration
                .GetSection("AWS:SQS:OutboundQueueNames")
                .Get<string[]>() ?? [];

            foreach (var queueName in outboundQueues.Where(q => !string.IsNullOrWhiteSpace(q)))
            {
                opts.PublishMessage<UserStatusEvent>().ToSqsQueue(queueName);
            }
        });
    }
}
