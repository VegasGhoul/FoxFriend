using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Lisichka {
    // A tiny supervised retrieval model, trained on the fox's own examples.
    // Character n-grams tolerate spelling variants; words carry more weight.
    // This is deliberately not advertised as an autoregressive neural LLM.
    public sealed class FoxMind {
        sealed class Sample { public string Label; public Dictionary<string,double> Vector; }
        public sealed class Match { public string Label; public double Score; }
        readonly List<Sample> samples=new List<Sample>();
        readonly Dictionary<string,double> idf=new Dictionary<string,double>();
        readonly Dictionary<string,string[]> answers=new Dictionary<string,string[]>();
        readonly Dictionary<string,int> previous=new Dictionary<string,int>();
        readonly Random random=new Random();
        string lastTopic="", name=""; int contextTurns;
        public void SetName(string value){name=value??"";}
        public int ExampleCount { get { return samples.Count; } }
        public int TopicCount { get { return answers.Count; } }
        public int FeatureCount { get { return idf.Count; } }
        public FoxMind() {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("FoxMind.corpus.tsv")) {
                if(stream==null)throw new InvalidDataException("FoxMind corpus resource missing");
                using(var reader=new StreamReader(stream))while(!reader.EndOfStream){
                    string line=reader.ReadLine();if(string.IsNullOrWhiteSpace(line)||line.StartsWith("#"))continue;
                    string[] fields=line.Split('\t');if(fields.Length!=3)throw new InvalidDataException("Invalid FoxMind corpus row");
                    answers[fields[0]]=fields[2].Split('|');
                    foreach(string example in fields[1].Split('|'))samples.Add(new Sample{Label=fields[0],Vector=Features(example)});
                }
            }
            foreach(var s in samples)foreach(string f in s.Vector.Keys) {double count;idf.TryGetValue(f,out count);idf[f]=count+1;}
            foreach(string f in idf.Keys.ToArray())idf[f]=Math.Log(1.0+samples.Count/(1.0+idf[f]))+1;
            foreach(var s in samples)Weight(s.Vector);
        }
        static Dictionary<string,double> Features(string text) {
            var result=new Dictionary<string,double>();
            foreach(string word in Intent.Normalize(text).Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries)) {
                if(word.Length==1)continue;
                if(Regex.IsMatch(word,@"^(?:очень|сильно|сегодня|сейчас|пожалуйста|вообще|прям|просто|немного|лисичка|лисонька)$"))continue;
                result["w:"+word]=2.5;
                string w="^"+word+"$";
                for(int length=3;length<=4;length++)for(int i=0;i+length<=w.Length;i++)result["c:"+w.Substring(i,length)]=.6;
            }
            return result;
        }
        void Weight(Dictionary<string,double> vector,bool keepUnknown=false) {
            double norm=0;foreach(string f in vector.Keys.ToArray()){double weight;if(!idf.TryGetValue(f,out weight)){if(!keepUnknown){vector.Remove(f);continue;}weight=3.5;}vector[f]*=weight;norm+=vector[f]*vector[f];}
            norm=Math.Sqrt(norm);if(norm>0)foreach(string f in vector.Keys.ToArray())vector[f]/=norm;
        }
        public Match Classify(string text) {
            var query=Features(text);
            // Correct a single insertion/deletion/substitution in a longer word
            // only when the learned vocabulary has exactly one nearest candidate.
            foreach(string f in query.Keys.Where(k=>k.StartsWith("w:")&&k.Length>=7&&!idf.ContainsKey(k)).ToArray()) {
                var candidates=idf.Keys.Where(k=>k.StartsWith("w:")&&OneEdit(f.Substring(2),k.Substring(2))).Take(2).ToArray();
                if(candidates.Length==1){query.Remove(f);query[candidates[0]]=2.5;}
            }
            Weight(query,true);if(query.Count==0)return null;
            var scores=new Dictionary<string,double>();
            foreach(var sample in samples){double score=0;foreach(var pair in query){double v;if(sample.Vector.TryGetValue(pair.Key,out v))score+=pair.Value*v;}double current;if(!scores.TryGetValue(sample.Label,out current)||score>current)scores[sample.Label]=score;}
            var sorted=scores.OrderByDescending(x=>x.Value).Take(2).ToArray();
            if(sorted.Length==0||sorted[0].Value<.48||(sorted.Length>1&&sorted[0].Value-sorted[1].Value<.045))return null;
            return new Match{Label=sorted[0].Key,Score=sorted[0].Value};
        }
        static bool OneEdit(string a,string b){if(Math.Abs(a.Length-b.Length)>1)return false;int i=0,j=0,edits=0;while(i<a.Length&&j<b.Length){if(a[i]==b[j]){i++;j++;continue;}if(++edits>1)return false;if(a.Length>=b.Length)i++;if(b.Length>=a.Length)j++;}return edits+(a.Length-i)+(b.Length-j)==1;}
        string Answer(string label){var options=answers[label];int old;previous.TryGetValue(label,out old);int next=options.Length==1?0:(old+1+random.Next(options.Length-1))%options.Length;previous[label]=next;return options[next];}
        public Reply Respond(string text) {
            string t=Intent.Normalize(text);
            var named=Regex.Match(t,@"^(?:меня зовут|зови меня|мое имя)\s+([а-яa-z]{2,24})$");
            if(named.Success){name=char.ToUpper(named.Groups[1].Value[0])+named.Groups[1].Value.Substring(1);return new Reply("Очень приятно, "+name+"! Я Лисичка 🧡 Запомню твоё имя на время нашей беседы.");}
            if(Regex.IsMatch(t,@"\b(?:как меня зовут|помнишь мое имя)\b"))return new Reply(name.Length==0?"Пока не знаю твоего имени. Как тебя называть? 🧡":"Тебя зовут "+name+" 🧡");
            if(Regex.IsMatch(t,@"\b(?:не\s+(?:очень\s+)?люблю\s+тебя|тебя\s+не\s+(?:очень\s+)?люблю|разлюбил\w*)\b"))return new Reply("Тебе не нужно говорить что-то из вежливости. Можем просто спокойно поболтать 🧡");
            if(Regex.IsMatch(text.Trim(),@"^(?:(?:🧡|❤\uFE0F?|♥\uFE0F?|💕|💖|💗|💛)\s*)+$"))return new Reply("Лови рыжее сердечко в ответ 🧡",null,true);
            // Negated requests must not trigger a hug or an affectionate animation.
            if(Regex.IsMatch(t,@"\bне\b.{0,20}\b(?:обним\w*|трог\w*|глад\w*|ласк\w*)\b"))return new Reply("Хорошо, без объятий. Побуду рядом тихонько 🧡");
            Match match=Classify(text);if(match==null){contextTurns++;return null;}
            string label=match.Label;
            bool intimate=label=="love"||label=="love_question";
            if(intimate && t.Split(' ').Length>1 && !Regex.IsMatch(t,@"\b(?:тебя|тибя|тебе|тобой|ты|лиса|лисичка|лисонька)\b"))return null;
            if(label=="love_question"&&!Regex.IsMatch(t,@"\bменя\b|\bя\s+тебе\b"))return null;
            if((label=="love"||label=="compliment")&&Regex.IsMatch(t,@"\b(?:не|нет)\b"))return null;
            // Explicit negative mood takes precedence over a superficially similar positive example.
            if(label=="happy"&&Intent.Negative(t))return null;
            if(label=="time")return new Reply("Сейчас "+DateTime.Now.ToString("HH:mm")+" по часам компьютера 🧡");
            if(label=="date")return new Reply("Сегодня "+DateTime.Now.ToString("dd.MM.yyyy")+" 🧡");
            if(label=="yes"||label=="no") {
                bool yes=label=="yes";
                if(contextTurns<4 && (lastTopic=="sad"||lastTopic=="anxious"||lastTopic=="lonely"))return new Reply(yes?"Тогда я слушаю. Что случилось? Можно начать с любого маленького кусочка 🧡":"Хорошо, рассказывать необязательно. Посидим спокойно 🧡");
                if(contextTurns<4 && lastTopic=="bored" && yes){lastTopic="game";return new Reply("Маленькая игра: назови три вещи вокруг тебя, которые похожи на лисичку по цвету 🧡");}
                return new Reply(yes?"Хорошо 🧡 О чём хочешь поговорить дальше?":"Поняла тебя. Можем сменить тему 🧡");
            }
            lastTopic=label;contextTurns=0;
            return new Reply(Answer(label),null,label=="love"||label=="love_question"||label=="hug"||label=="compliment");
        }
    }
}
