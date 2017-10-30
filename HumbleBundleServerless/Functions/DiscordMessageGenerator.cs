using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using HumbleBundleBot;
using HumbleBundleServerless.Models;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace HumbleBundleServerless
{
    public static class DiscordMessageGenerator
    {
        [FunctionName("DiscordMessageGenerator")]
        public static void Run(
            [QueueTrigger("bundlequeue")] BundleQueue queuedBundle,
            [Queue("messagequeue")] ICollector<DiscordMessage> messageQueue,
            [Table("webhookRegistration")] IQueryable<WebhookRegistrationEntity> existingWebhooks,
            TraceWriter log)
        {
            log.Info($"C# Queue trigger function processed: {queuedBundle.Bundle.Name}");

            var bundle = queuedBundle.Bundle;

            var webhooks = GetAllWebhooksForBundleType(existingWebhooks, queuedBundle.Type, queuedBundle.IsUpdate);

            log.Info($"Found {webhooks.Count} webhooks for type {queuedBundle.Type}");

            var content = "New Bundle:";

            if (queuedBundle.IsUpdate)
            {
                content = "Bundle Updated:";
            }

            var message = new DiscordWebhookPayload
            {
                content = content,
                embeds = new List<DiscordEmbed>()
            };

            message.embeds.Add(new DiscordEmbed()
            {
                url = bundle.URL,
                title = bundle.Description,
                image = new ImageField()
                {
                    url = bundle.ImageUrl
                },
                author = new AuthorField()
                {
                    name = "Humble Bundle",
                    url = bundle.URL
                }
            });

            foreach (var section in bundle.Sections)
            {
                var embed = new DiscordEmbed
                {
                    title = section.Title,
                    url = bundle.URL,
                    description = ""
                };

                foreach (var item in section.Items)
                {
                    embed.description += item.Name + "\n";
                }

                message.embeds.Add(embed);
            }

            foreach (var webhook in webhooks)
            {
                messageQueue.Add(new DiscordMessage
                {
                    WebhookUrl = webhook,
                    Payload = message
                });
            }
        }

        private static List<String> GetAllWebhooksForBundleType(IQueryable<WebhookRegistrationEntity> existingWebhooks, BundleTypes type, bool isUpdate)
        {
            var webhooksForType = existingWebhooks.Where(x => x.PartitionKey == type.ToString());

            if (isUpdate)
            {
                webhooksForType = webhooksForType.Where(x => x.ShouldRecieveUpdates == true);
            }

            return  webhooksForType.ToList().Select(x => x.GetDecryptedWebhook()).ToList();
        }
    }
}
