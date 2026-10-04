using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace Lisichka {
    public enum SendMode { Enter, Arrow, Both }
    public static class InputPolicy {
        public static bool EnterSends(SendMode mode,bool shift){return !shift&&mode!=SendMode.Arrow;}
        public static bool ArrowSends(SendMode mode){return mode!=SendMode.Enter;}
        public static double Scale(double value){return double.IsNaN(value)||double.IsInfinity(value)?1:Math.Max(.6,Math.Min(1.4,value));}
    }
    public static class Names {
        static readonly Dictionary<string,string> Tender=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
            {"Кирилл","Кирюша"},{"Кирюша","Кирюша"},{"Александр","Сашенька"},{"Александра","Сашенька"},{"Саша","Сашенька"},{"Анна","Анечка"},{"Аня","Анечка"},{"Мария","Машенька"},{"Маша","Машенька"},{"Анастасия","Настенька"},{"Настя","Настенька"},{"Екатерина","Катюша"},{"Катя","Катюша"},{"Дмитрий","Димочка"},{"Дима","Димочка"},{"Михаил","Мишенька"},{"Миша","Мишенька"},{"Сергей","Серёжа"},{"Андрей","Андрюша"},{"Елена","Леночка"},{"Лена","Леночка"},{"Ольга","Олечка"},{"Оля","Олечка"},{"Дарья","Дашенька"},{"Даша","Дашенька"},{"Иван","Ванюша"},{"Ваня","Ванюша"},{"Никита","Никитушка"},{"Алексей","Алёша"},{"Леша","Алёша"},{"Татьяна","Танечка"},{"Таня","Танечка"},{"Юлия","Юленька"},{"Юля","Юленька"}};
        public static string Clean(string value){value=(value??"").Trim();if(!Regex.IsMatch(value,@"^[\p{L}][\p{L}\- ]{1,31}$"))return "";return char.ToUpper(value[0])+value.Substring(1);}
        public static string Read(string text,bool awaiting){
            string t=(text??"").Trim().Trim('.','!','?');var m=Regex.Match(t,@"^(?:меня зовут|зови меня|мое имя|моё имя)\s+(.+)$",RegexOptions.IgnoreCase);
            if(m.Success)return Clean(m.Groups[1].Value);
            if(!awaiting||Regex.IsMatch(t,@"\s")||Regex.IsMatch(t,@"^(?:привет\w*|пока|нет|да|ага|спасибо|неа|хорошо|плохо|потом|пропустить|сядь|полежи|спи|лисичка)$",RegexOptions.IgnoreCase))return "";
            return Clean(t);
        }
        public static string Form(string name,string custom,bool affectionate){if(!affectionate)return name;string clean=Clean(custom);if(clean.Length>0)return clean;string result;return Tender.TryGetValue(name,out result)?result:name;}
        public static string Address(string reply,string name){if(string.IsNullOrEmpty(name)||string.IsNullOrEmpty(reply))return reply;return name+", "+char.ToLower(reply[0])+reply.Substring(1);}
    }
    // Pure drag arithmetic: no nested native drag loop, valid after any sequence
    // of button/capture events. Screen coordinates are converted to WPF DIPs.
    public sealed class DragSession {
        double mouseX,mouseY,originX,originY;
        public bool Active {get;private set;} public bool Moved {get;private set;}
        public void Start(double x,double y,double left,double top){mouseX=x;mouseY=y;originX=left;originY=top;Active=true;Moved=false;}
        public bool Update(double x,double y,bool pressed,out double left,out double top){left=originX;top=originY;if(!Active)return false;if(!pressed){Active=false;return false;}double dx=x-mouseX,dy=y-mouseY;if(Math.Abs(dx)+Math.Abs(dy)>4)Moved=true;if(!Moved)return false;left=originX+dx;top=originY+dy;return true;}
        public void Finish(){Active=false;}
    }
}
