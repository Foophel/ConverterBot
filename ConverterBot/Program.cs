using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConverterBot
{
    class Program
    {
        static Dictionary<long, string[]> state = new Dictionary<long, string[]>();

        static async Task Main(string[] args)
        {
            string botToken = "ТОКЕНТУТ";
            var bot = new TelegramBotClient(botToken);
            using var cts = new CancellationTokenSource();
            var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };

            bot.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );

            var me = await bot.GetMe();
            Console.WriteLine($"Бот запущен!");
            Console.ReadLine();
            cts.Cancel();
        }

        static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            if (update.Message is not { Text: { } text } message) return;
            long chatId = message.Chat.Id;

            if (!state.ContainsKey(chatId))
                state[chatId] = new[] { "", "", "", "cat" };

            var s = state[chatId];

            //старт
            if (text == "/start")
            {
                state[chatId] = new[] { "", "", "", "cat" };
                await botClient.SendMessage(chatId, "Выбери категорию:", replyMarkup: MainKeyboard(), cancellationToken: ct);
                return;
            }

            //категория
            if (s[3] == "cat")
            {
                if (text == "Длина") s[0] = "length";
                else if (text == "Масса") s[0] = "mass";
                else if (text == "Температура") s[0] = "temp";
                else if (text == "Время") s[0] = "time";
                else
                {
                    // Если написал что-то не то
                    await botClient.SendMessage(chatId, "Пожалуйста, выбери категорию из кнопок ниже:", replyMarkup: MainKeyboard(), cancellationToken: ct);
                    return;
                }

                s[3] = "from";
                await botClient.SendMessage(chatId, "Выбери ИСХОДНУЮ единицу:", replyMarkup: UnitsKeyboard(s[0]), cancellationToken: ct);
                return;
            }

            //исходник
            if (s[3] == "from")
            {
                if (IsValidUnit(s[0], text))
                {
                    s[1] = text;
                    s[3] = "to";
                    await botClient.SendMessage(chatId, $"Исходная: {s[1]}\nТеперь выбери ЦЕЛЕВУЮ единицу:", replyMarkup: UnitsKeyboard(s[0]), cancellationToken: ct);
                }
                else
                {
                    await botClient.SendMessage(chatId, "Пожалуйста, выбери единицу из кнопок ниже:", replyMarkup: UnitsKeyboard(s[0]), cancellationToken: ct);
                }
                return;
            }

            //целевая
            if (s[3] == "to")
            {
                if (IsValidUnit(s[0], text))
                {
                    s[2] = text;
                    s[3] = "val";
                    //Скрыть клаву
                    await botClient.SendMessage(chatId, $"{s[1]} → {s[2]}\nНапиши число:", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
                }
                else
                {
                    await botClient.SendMessage(chatId, "Пожалуйста, выбери единицу из кнопок ниже:", replyMarkup: UnitsKeyboard(s[0]), cancellationToken: ct);
                }
                return;
            }

            //ввод числа
            if (s[3] == "val")
            {
                string cleanText = text.Replace(',', '.');
                if (!double.TryParse(cleanText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double v))
                {
                    //если не число
                    await botClient.SendMessage(chatId, "Это не число. Попробуй еще раз.", cancellationToken: ct);
                    return;
                }
                double r = ConvertUnits(s[0], s[1], s[2], v);
                await botClient.SendMessage(chatId, $"{v} {s[1]} = {r:F4} {s[2]}", cancellationToken: ct);
                state[chatId] = new[] { "", "", "", "cat" };
                await botClient.SendMessage(chatId, "Выбери категорию:", replyMarkup: MainKeyboard(), cancellationToken: ct);
                return;
            }
        }

        //првоерка на введенный текст
        static bool IsValidUnit(string cat, string text)
        {
            if (cat == "length") return text == "мм" || text == "см" || text == "дм" || text == "м" || text == "км";
            if (cat == "mass") return text == "мг" || text == "г" || text == "кг" || text == "ц" || text == "т";
            if (cat == "time") return text == "мс" || text == "с" || text == "мин" || text == "час";
            if (cat == "temp") return text == "C" || text == "F" || text == "K";
            return false;
        }
        static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
        {
            Console.WriteLine("Ошибка: " + exception.Message);
            return Task.CompletedTask;
        }
        static double ConvertUnits(string cat, string from, string to, double v)
        {
            if (cat == "temp")
            {
                double c = from == "C" ? v : from == "F" ? (v - 32) * 5 / 9 : v - 273.15;
                return to == "C" ? c : to == "F" ? c * 9 / 5 + 32 : c + 273.15;
            }
            var u = new Dictionary<string, Dictionary<string, double>>()
            {
                ["length"] = new() { ["мм"] = 0.001, ["см"] = 0.01, ["дм"] = 0.1, ["м"] = 1, ["км"] = 1000 },
                ["mass"] = new() { ["мг"] = 0.001, ["г"] = 1, ["кг"] = 1000, ["ц"] = 100000, ["т"] = 1000000 },
                ["time"] = new() { ["мс"] = 0.001, ["с"] = 1, ["мин"] = 60, ["час"] = 3600 }
            };
            return v * u[cat][from] / u[cat][to];
        }

        //клава
        static ReplyKeyboardMarkup MainKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "Длина", "Масса" },
                new KeyboardButton[] { "Температура", "Время" }
            })
            { ResizeKeyboard = true };
        }
        static ReplyKeyboardMarkup UnitsKeyboard(string cat)
        {
            if (cat == "length") return new ReplyKeyboardMarkup(new[] { new KeyboardButton[] { "мм", "см", "дм", "м", "км" } }) { ResizeKeyboard = true };
            if (cat == "mass") return new ReplyKeyboardMarkup(new[] { new KeyboardButton[] { "мг", "г", "кг", "ц", "т" } }) { ResizeKeyboard = true };
            if (cat == "time") return new ReplyKeyboardMarkup(new[] { new KeyboardButton[] { "мс", "с", "мин", "час" } }) { ResizeKeyboard = true };
            if (cat == "temp") return new ReplyKeyboardMarkup(new[] { new KeyboardButton[] { "C", "F", "K" } }) { ResizeKeyboard = true };
            return MainKeyboard();
        }
    }
}