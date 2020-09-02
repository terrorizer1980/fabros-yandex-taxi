using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;

namespace FabrosYandexTaxi
{
    public static class Program
    {
        public static void Main()
        {
            Process.Start("/bin/bash", "-c \"uname -m\"");
            return;
            try
            {
                Run();
            }
            catch (Exception e)
            {
                SendTelegramTextMessage("Ошибка при обработке писем.", e.Message);
                throw;
            }
        }

        private static void Run()
        {
            var tempDirectory = Path.GetTempPath();
            var baseDirectory = Path.Combine(tempDirectory, Guid.NewGuid().ToString());
            Directory.CreateDirectory(baseDirectory);
            var endDate = DateTime.UtcNow;
            var fromDate = endDate.AddMonths(-1);
            var messages = LoadMessages(fromDate);
            var total = messages.Sum(m => m.Cost);
            
            var zipStream = new MemoryStream();
            using (var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var message in messages)
                {
                    var imageStream = new MemoryStream();
                    message.GenerateImage(imageStream);
                    imageStream.Position = 0;
                    var pngName = message.Date.ToString("dd-MMM-yyyy HH-mm-ss") + ".png";
                    var entry = zipArchive.CreateEntry(pngName);
                    using var entryStream = entry.Open();
                    imageStream.CopyTo(entryStream);
                }
            }

            zipStream.Position = 0;
            
            SendTelegramDocumentMessage(
                zipStream,
                "Screenshots.zip",
                $"Отчёт о поездках",
                $"за период {fromDate:dd-MMM-yyyy} - {endDate:dd-MMM-yyyy}",
                $"Сумма {total} BYN"
            );
        }

        private static List<TaxiMessage> LoadMessages(DateTime fromDate)
        {
            var login = Environment.GetEnvironmentVariable("GMAIL_LOGIN");
            var password = Environment.GetEnvironmentVariable("GMAIL_PASSWORD");
            var imapClient = new ImapClient();
            imapClient.Connect("imap.gmail.com", 993);
            imapClient.Authenticate(login, password);
            var mailbox = imapClient.Inbox;
            mailbox.Open(FolderAccess.ReadOnly);
            var searchQuery = SearchQuery.SentSince(fromDate)
                .And(SearchQuery.FromContains("no-reply@taxi.yandex.com"))
                .And(SearchQuery.Or(
                    SearchQuery.BodyContains(TaxiMessage.ValidStreetNames[0]),
                    SearchQuery.BodyContains(TaxiMessage.ValidStreetNames[1]))
                );
            var messages = mailbox.Search(searchQuery)
                .Select(id => mailbox.GetMessage(id))
                .ToList();
            mailbox.Close();
            return messages.Select(m => new TaxiMessage(m))
                .Where(m => m.HasOfficeTaxi)
                .ToList();
        }

        private static void SendTelegramDocumentMessage(Stream stream, string name, params string[] textLines)
        {
            SendTelegramDocumentMessage(stream, name, JoinTextMessage(textLines));
        }

        private static void SendTelegramDocumentMessage(Stream stream, string name, string text)
        {
            var botClient = CreateTelegramBotClient();
            var chatId = CreateTelegramChatId();
            var file = new InputOnlineFile(stream, name);
            botClient.SendDocumentAsync(chatId, file, text).Wait();
        }

        private static void SendTelegramTextMessage(params string[] textLines)
        {
            SendTelegramTextMessage(JoinTextMessage(textLines));
        }

        private static void SendTelegramTextMessage(string text)
        {
            var botClient = CreateTelegramBotClient();
            var chatId = CreateTelegramChatId();
            botClient.SendTextMessageAsync(chatId, text).Wait();
        }

        private static string JoinTextMessage(string[] textLines)
        {
            var sb = new StringBuilder();
            foreach (var textLine in textLines.Take(textLines.Length - 1))
            {
                sb.AppendLine(textLine);
            }

            sb.Append(textLines.Last());
            return sb.ToString();
        }

        private static TelegramBotClient CreateTelegramBotClient()
        {
            var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
            return new TelegramBotClient(botToken);
        }

        private static ChatId CreateTelegramChatId()
        {
            var userId = Environment.GetEnvironmentVariable("TELEGRAM_USER_ID");
            return new ChatId(int.Parse(userId));
        }
    }
}