using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Lisichka {
    public enum Pose { Sit, Lie, Sleep }
    public class Reply {
        public string Text; public Pose? Action; public bool Affection;public int TailAction;
        public Reply(string text, Pose? action = null, bool affection = false) { Text=text; Action=action; Affection=affection; }
    }
    public static class Intent {
        public static string Normalize(string text) { return Regex.Replace(Regex.Replace((text??"").ToLowerInvariant().Replace('ё','е'), @"[^а-яa-z0-9\s]", " "),@"\s+"," ").Trim(); }
        static bool Has(string text, string pattern) { return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase); }
        public static Pose? Command(string original) {
            string t=Normalize(original);
            // Process clauses independently, so "не спи, а сядь" still works.
            foreach (string clause in Regex.Split(t, @"\b(?:а|но|зато)\b")) {
                string s=clause.Trim();
                if (Has(s,@"\bне\s+(?:спи|засыпай|дремли)\b|\b(?:перестань|прекрати|хватит)\s+(?:спать|дремать)\b")) return Pose.Sit;
                if (Has(s,@"\b(?:не|нельзя)\b.{0,26}\b(?:спать|поспать|засып\w*|дрем\w*)")) continue;
                if (Has(s,@"\b(?:не|нельзя|не\s+надо|не\s+нужно)\b.{0,26}\b(?:сад\w*|сядь|сид\w*|лож\w*|ляг\w*|леж\w*|полеж\w*|уклад\w*|отдых\w*)")) continue;
                if (Has(s,@"\b(?:проснись|просыпайся|вставай|разбуд\w*|подъем)\b")) return Pose.Sit;
                if (Has(s,@"\b(?:я|мне)\b.{0,16}\b(?:хочу|хочется|пора|нужно|надо)\b.{0,12}\b(?:спать|сесть|лечь)\b") && !Has(s,@"\b(?:ты|тебе|тоже)\b")) continue;
                if (Has(s,@"\b(?:спи|засыпай|дремли|поспи|баю|баюшки)\b|\b(?:ложись|иди|пора|давай|отправляйся|тебе|можешь|хочу\s+чтобы\s+ты)\b.{0,35}\b(?:спать|поспать|сон|кроват\w*)\b|\bспокойной\s+ночи\b")) return Pose.Sleep;
                if (Has(s,@"\b(?:сядь|садись|присядь|посиди|сиди|присаживайся)\b|\b(?:давай|можешь|тебе|хочу\s+чтобы\s+ты)\b.{0,30}\b(?:сесть|сидеть|посидеть)\b")) return Pose.Sit;
                if (Has(s,@"\b(?:ляг|ляжь|ложись|полежи|лежи|приляг|отдохни|укладывайся)\b|\b(?:давай|можешь|тебе|хочу\s+чтобы\s+ты)\b.{0,30}\b(?:лечь|полеж\w*|лежать|отдохнуть)\b")) return Pose.Lie;
            }
            return null;
        }
        public static bool Negative(string t) { t=Normalize(t); return Has(t,@"\b(?:плох\w*|груст\w*|тяжел\w*|тяжко|одинок\w*|одиноч\w*|устал\w*|тревож\w*|страшно|плачу|печал\w*|ужас\w*|отврат\w*|расстро\w*|паршив\w*|выгор\w*|боюсь|больно|боль\w*)\b|\bне\s+(?:очень|хорошо|рад\w*|весело|вывожу|справляюсь)|\bнет\s+(?:сил|настроения)|\bвсе\s+(?:бесит|достало)" ) && !Has(t,@"\b(?:не|перестал\w*)\s+(?:груст\w*|плох\w*|тревож\w*|расстро\w*)"); }
        public static bool Positive(string t) { t=Normalize(t); return Has(t,@"\b(?:хорошо|отлично|прекрасно|супер|замечательно|счастлив\w*|радуюсь|радост\w*|получилось|классно|здорово|лучше|неплохо|нормально)\b") && !Has(t,@"\bне\s+(?:очень\s+)?(?:хорошо|отлично|лучше|получилось)"); }
    }
    public class Companion {
        readonly Random random=new Random(); readonly FoxMind mind=new FoxMind();readonly ConversationAnalyzer analyzer=new ConversationAnalyzer();readonly DialogueRules dialogue=new DialogueRules(); string mood="neutral"; bool asked;
        public void SetName(string name){mind.SetName(name);}
        string Pick(params string[] s) { return s[random.Next(s.Length)]; }
        public static string Greeting(DateTime now) {
            if(now.Hour>=5 && now.Hour<12) return "Доброе утро! 🧡";
            if(now.Hour>=12 && now.Hour<18) return "Добрый день! 🧡";
            if(now.Hour>=18 && now.Hour<23) return "Добрый вечер! 🧡";
            return "Доброй ночи! 🧡";
        }
        public static bool Night(DateTime now) { return now.Hour<7; }
        public string CheckIn() { asked=true; return Pick("Как дела? 🧡 Я устроилась рядом и готова послушать.","Как твоё настроение? Хочешь рассказать, как прошёл день? 🧡"); }
        public Reply Respond(string text) {
            Reply conversational=dialogue.Respond(text);if(conversational!=null){if(!conversational.Action.HasValue)conversational.Action=Intent.Command(text);if(text.Length>250&&!conversational.Action.HasValue&&conversational.TailAction==0){var details=analyzer.Respond(text);if(details!=null)conversational.Text+="\n"+details.Text;}return conversational;}
            Reply analyzed=analyzer.Respond(text);if(analyzed!=null){dialogue.ClearContext();analyzed.Action=Intent.Command(text);return analyzed;}
            Pose? p=Intent.Command(text);
            if(p==Pose.Sleep) return new Reply("Свернусь клубочком… Сладких снов, если тоже собираешься отдыхать 🧡",p);
            if(p==Pose.Lie) return new Reply("Устроюсь поудобнее рядом с тобой 🧡",p);
            if(p==Pose.Sit) return new Reply("Вот и я, сижу рядом и слушаю 🧡",p);
            string t=Intent.Normalize(text);
            Reply learned=mind.Respond(text);
            if(learned!=null){dialogue.ClearContext();if(Intent.Negative(t))mood="sad";else if(Intent.Positive(t))mood="happy";return learned;}
            if(Regex.IsMatch(t,@"\b(?:поглад\w*|обним\w*|обнять|почеш\w*|ласк\w*)\b")) return new Reply("Мр-р… Спасибо за тепло 🧡",null,true);
            if(Intent.Negative(t)) {
                mood="sad"; asked=true;
                if(Regex.IsMatch(t,@"устал|нет сил|выгор")) return new Reply("Похоже, сил сейчас совсем мало. Не нужно решать всё сразу. Может, устроим маленькую передышку? Что сегодня больше всего тебя вымотало? 🧡");
                if(Regex.IsMatch(t,@"одинок|одиноч")) return new Reply("Чувствовать одиночество бывает больно. Я могу послушать, а ещё можно написать человеку, с которым тебе спокойно. Хочешь рассказать, чего тебе сейчас не хватает? 🧡");
                return new Reply(Pick("Жаль, что тебе сейчас тяжело. Твои чувства важны. Хочешь выговориться или лучше просто немного побудем рядом? 🧡","Не нужно заставлять себя улыбаться. Расскажи, что случилось, если хочется. Давай разберёмся по одному маленькому шагу 🧡"));
            }
            if(Intent.Positive(t)) { mood="happy"; asked=false; return new Reply(Pick("Как приятно это слышать! 🧡 Что сегодня порадовало тебя больше всего?","Рада за тебя! Пусть это тепло останется с тобой подольше 🧡","Ура, шевелю ушками от радости! Хочешь поделиться хорошим моментом? 🧡")); }
            if(Regex.IsMatch(t,@"\b(?:спасибо|благодар\w*)\b")) return new Reply("Пожалуйста 🧡 Можем ещё поболтать или просто посидеть в тишине.");
            if(Regex.IsMatch(t,@"\b(?:привет|здравствуй|доброе утро|добрый вечер|добрый день)\b")) return new Reply(Greeting(DateTime.Now)+" Как ты?");
            if(Regex.IsMatch(t,@"\b(?:помощь|команды|что ты умеешь)\b")) return new Reply("Попроси: «присядь», «полежи рядом», «пора спать» или «просыпайся». Нажми на меня, чтобы погладить, потяни — чтобы перенести. Свободный ИИ-разговор включается в настройках 🧡");
            if(Regex.IsMatch(t,@"\b(?:как ты|как твои дела|как дела)\b")) return new Reply("Устроилась уютно и грею лапки 🧡 А как у тебя проходит день?");
            if(Regex.IsMatch(t,@"\b(?:молчи|тихо|тишин\w*|побудь рядом)\b")) return new Reply("Хорошо, просто посижу рядом 🧡");
            if(Regex.IsMatch(t,@"\b(?:я хочу спать|мне пора спать|я устал\w*)\b")) return new Reply("Можно позволить себе отдохнуть. Пусть сон будет спокойным и тёплым 🧡");
            if(mood=="sad") { if(Regex.IsMatch(t,@"\b(?:нет|не хочу|не надо)\b")) { asked=false; return new Reply("Хорошо, рассказывать необязательно. Побуду рядом 🧡"); } return new Reply(Pick("Спасибо, что делишься. Что сейчас было бы полезнее: чтобы тебя выслушали или вместе придумали маленький следующий шаг? 🧡","Слышу тебя. Что в этой ситуации задевает сильнее всего? Можно рассказать столько, сколько хочется 🧡")); }
            if(asked) { asked=false; return new Reply("Спасибо, что ответил. Хочешь рассказать чуть подробнее — что сегодня запомнилось? 🧡"); }
            return new Reply(Pick("Не совсем поняла эту фразу. Скажешь немного иначе? Можно рассказать о настроении, попросить поддержки или шутку 🧡","Пока не знаю, что ответить на это. Давай попробуем другими словами? Я училась на небольшом наборе лисьих разговоров 🧡"));
        }
    }
}
